#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Wargon.Nukecs.HotReload
{
    public static class HotReloadCompiler
    {
        private static string _cscPath;
        private static string _cscDllPath;
        private static bool _isDotnet;
        private static string _tempDir;
        private static volatile int _compileCount;
        private static volatile bool _isCompiling;
        private static Dictionary<string, MethodInfo> _systemMethodCache;
        private static List<string> _cachedReferences;
        private static int _cachedReferencesAssemblyCount;

        public static event Action<string, MethodInfo[], Func<IntPtr, Threads, ISystemRunner>[]> OnSystemsCompiled;

        public static void PrewarmCache()
        {
            Task.Run(static () =>
            {
                EnsureSystemMethodCache();
                FindCsc();
                CollectReferences();
                HotReloadRoslynCompiler.Initialize();
            });
        }

        public static string FindCsc()
        {
            if (_cscPath != null && File.Exists(_cscPath)) return _cscPath;

            var editorPath = EditorApplication.applicationPath;
            var dataPath = editorPath.Replace(".exe", "") + "/Data";
            if (!Directory.Exists(dataPath))
                dataPath = Path.Combine(Path.GetDirectoryName(editorPath)!, "Data");

            var dotnetExeCandidates = new[]
            {
                Path.Combine(dataPath, "NetCoreRuntime", "dotnet.exe"),
            };

            var cscDllCandidates = new[]
            {
                Path.Combine(dataPath, "DotNetSdkRoslyn", "csc.dll"),
                Path.Combine(dataPath, "Roslyn", "csc.dll"),
                Path.Combine(dataPath, "Tools", "Roslyn-csc", "csc.dll"),
            };

            var cscExeCandidates = new[]
            {
                Path.Combine(dataPath, "Roslyn", "csc.exe"),
                Path.Combine(dataPath, "Tools", "Roslyn-csc", "csc.exe"),
                Path.Combine(dataPath, "Tools", "Roslyn", "csc.exe"),
            };

            foreach (var c in cscExeCandidates)
            {
                if (File.Exists(c))
                {
                    _cscPath = c;
                    _isDotnet = false;
                    return _cscPath;
                }
            }

            var dotnetExe = dotnetExeCandidates.FirstOrDefault(File.Exists);
            if (dotnetExe != null)
            {
                var cscDll = cscDllCandidates.FirstOrDefault(File.Exists);
                if (cscDll != null)
                {
                    _cscPath = dotnetExe;
                    _cscDllPath = cscDll;
                    _isDotnet = true;
                    return _cscPath;
                }
            }

            return null;
        }

        public static void CompileAndReload(string sourceFilePath)
        {
            if (_isCompiling)
            {
                Debug.LogWarning("[HotReload] Compilation already in progress, skipping...");
                return;
            }

            var sw = Stopwatch.StartNew();

            HotReloadRoslynCompiler.Initialize();

            var roslynAvailable = HotReloadRoslynCompiler.IsAvailable;
            if (!roslynAvailable)
            {
                var cscCheck = FindCsc();
                if (cscCheck == null)
                {
                    Debug.LogError("[HotReload] No compiler available (Roslyn not found, csc.exe not found)");
                    return;
                }
            }

            var sourceCode = File.ReadAllText(sourceFilePath);
            var methods = FindSystemMethods(sourceCode);
            if (methods == null || methods.Length == 0)
            {
                Debug.Log($"[HotReload] No [System] methods found in {Path.GetFileName(sourceFilePath)}");
                return;
            }

            var methodsMs = sw.ElapsedMilliseconds;

            var usings = ExtractUsings(sourceCode);

            _tempDir = Path.Combine(Path.GetTempPath(), "NukecsHotReload");
            if (!Directory.Exists(_tempDir))
                Directory.CreateDirectory(_tempDir);

            var count = System.Threading.Interlocked.Increment(ref _compileCount);

            var wrapperSource = GenerateJobAndFactory(methods, usings, count, sourceCode);
            var wrapperFile = Path.Combine(_tempDir, $"Wrapper_{count}.cs");
            var outputFile = Path.Combine(_tempDir, $"HotReload_{count}.dll");

            File.WriteAllText(wrapperFile, wrapperSource);

            var genMs = sw.ElapsedMilliseconds;

            var references = CollectReferences();

            _isCompiling = true;

            Task.Run(() =>
            {
                try
                {
                    byte[] dllBytes = null;
                    var compilerUsed = "roslyn";

                    if (roslynAvailable)
                    {
                        HotReloadRoslynCompiler.BuildMetadataReferences(references);
                        dllBytes = HotReloadRoslynCompiler.Compile(wrapperSource, sourceCode, $"HotReload_{count}");
                    }

                    var compileMs = sw.ElapsedMilliseconds;

                    if (dllBytes == null)
                    {
                        compilerUsed = "csc";
                        var csc = FindCsc();
                        if (csc == null)
                        {
                            EditorApplication.delayCall += () =>
                            {
                                Debug.LogError("[HotReload] No compiler available");
                            };
                            return;
                        }

                        var args = BuildArgs(csc, outputFile, new[] { wrapperFile, sourceFilePath }, references);
                        var (exitCode, stderr, stdout) = RunProcessBackground(csc, args);
                        compileMs = sw.ElapsedMilliseconds;

                        if (exitCode != 0)
                        {
                            EditorApplication.delayCall += () =>
                            {
                                var errorOutput = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                                Debug.LogError($"[HotReload] Compilation failed (exit {exitCode}):\n{errorOutput}");
                            };
                            return;
                        }

                        dllBytes = File.ReadAllBytes(outputFile);
                    }

                    var loadMs = sw.ElapsedMilliseconds;

                    EditorApplication.delayCall += () =>
                    {
                        var assembly = Assembly.Load(dllBytes);
                        var factories = CreateFactories(assembly, methods, count);
                        if (factories != null)
                        {
                            OnSystemsCompiled?.Invoke(sourceFilePath, methods, factories);
                            var totalMs = sw.ElapsedMilliseconds;
                            Debug.Log($"[HotReload] Compiled {methods.Length} system(s) from {Path.GetFileName(sourceFilePath)} in {totalMs}ms ({compilerUsed}: methods: {methodsMs}ms, gen: {genMs - methodsMs}ms, compile: {compileMs - genMs}ms, load: {loadMs - compileMs}ms)");
                        }
                    };
                }
                finally
                {
                    _isCompiling = false;
                }
            });
        }

        private static void EnsureSystemMethodCache()
        {
            if (_systemMethodCache != null) return;
            _systemMethodCache = new Dictionary<string, MethodInfo>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                               BindingFlags.Static))
                        {
                            if (method.GetCustomAttributes(typeof(SystemAttribute), false).Length > 0)
                            {
                                var key = $"{type.Name}.{method.Name}";
                                _systemMethodCache.TryAdd(key, method);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private static MethodInfo[] FindSystemMethods(string sourceCode)
        {
            EnsureSystemMethodCache();

            var methods = new List<MethodInfo>();
            var seen = new HashSet<string>();

            foreach (var kv in _systemMethodCache)
            {
                var method = kv.Value;
                if (sourceCode.Contains(method.Name) && sourceCode.Contains(method.DeclaringType!.Name) && seen.Add(kv.Key))
                    methods.Add(method);
            }

            return methods.ToArray();
        }

        private static string[] ExtractUsings(string sourceCode)
        {
            var usings = new List<string>();
            foreach (var line in sourceCode.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
                {
                    usings.Add(trimmed);
                }
                else if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("//") && !trimmed.StartsWith("using "))
                {
                    break;
                }
            }
            return usings.ToArray();
        }

        private static string GenerateJobAndFactory(MethodInfo[] methods, string[] fileUsings, int compileCount, string sourceCode)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using Unity.Collections.LowLevel.Unsafe;");
            sb.AppendLine("using Unity.Jobs;");
            sb.AppendLine("using Unity.Jobs.LowLevel.Unsafe;");
            sb.AppendLine("using Wargon.Nukecs;");
            var addedNamespaces = new HashSet<string>();
            foreach (var u in fileUsings)
            {
                if (u != "using System;" && !u.Contains("Runtime.CompilerServices") &&
                    !u.Contains("using Wargon.Nukecs;"))
                {
                    sb.AppendLine(u);
                    var ns = u.Trim().TrimEnd(';').Replace("using ", "").Trim();
                    addedNamespaces.Add(ns);
                }
            }
            foreach (var method in methods)
            {
                var ns = method.DeclaringType?.Namespace;
                if (!string.IsNullOrEmpty(ns) && addedNamespaces.Add(ns))
                    sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();

            sb.AppendLine("namespace Wargon.Nukecs.HotReload.Wrappers");
            sb.AppendLine("{");

            for (int mi = 0; mi < methods.Length; mi++)
            {
                var method = methods[mi];
                var containingTypeName = method.DeclaringType?.Name ?? "";
                var methodName = method.Name;
                var generatedName = $"{containingTypeName}_{methodName}";
                var jobName = $"{generatedName}_HR_Job_{compileCount}";
                var runnerName = $"{generatedName}_HR_Runner_{compileCount}";
                var factoryName = $"{generatedName}_HR_Factory_{compileCount}";
                var declaringTypeFullName = GetFullTypeName(method.DeclaringType!);

                var sourceParams = ParseMethodParamsFromSource(sourceCode, methodName);

                if (sourceParams != null)
                {
                    GenerateSelfContainedRunner(sb, sourceParams, jobName, runnerName, factoryName, declaringTypeFullName, methodName);
                }
                else
                {
                    GenerateLegacyRunner(sb, method, jobName, generatedName, factoryName, declaringTypeFullName, methodName, compileCount);
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void GenerateSelfContainedRunner(
            StringBuilder sb, SourceMethodParam[] sourceParams,
            string jobName, string runnerName, string factoryName,
            string declaringTypeFullName, string methodName)
        {
            var queryParamIndex = -1;
            var queryType = "";
            var queryParamName = "";
            var hasStateParam = false;
            var stateParamName = "state";

            for (int i = 0; i < sourceParams.Length; i++)
            {
                var p = sourceParams[i];
                if (queryParamIndex < 0 && IsQueryType(p.TypeName))
                {
                    queryParamIndex = i;
                    queryType = p.TypeName;
                    queryParamName = p.Name;
                }
                else if (IsStateType(p.TypeName))
                {
                    hasStateParam = true;
                    stateParamName = p.Name;
                }
            }

            var hasQuery = queryParamIndex >= 0;

            var eventsParamIndex = -1;
            for (int i = 0; i < sourceParams.Length; i++)
            {
                if (i != queryParamIndex && IsEventsType(sourceParams[i].TypeName))
                {
                    eventsParamIndex = i;
                    break;
                }
            }
            var hasEvents = eventsParamIndex >= 0;
            var isEventsDriven = hasEvents && !hasQuery;
            var eventsParamName = hasEvents ? sourceParams[eventsParamIndex].Name : "";

            var onUpdateParamsList = new List<string>();
            var onUpdateCallArgsList = new List<string>();
            var systemParamFields = new List<(string name, string type)>();
            var systemParamUpdateBatchedList = new List<string>();
            var systemParamUpdateParallelList = new List<string>();

            for (int i = 0; i < sourceParams.Length; i++)
            {
                var p = sourceParams[i];
                var modifier = p.IsByRef ? "ref " : "";

                if (i == queryParamIndex)
                {
                    onUpdateParamsList.Add($"ref {p.TypeName} {p.Name}");
                    onUpdateCallArgsList.Add($"ref {p.Name}");
                    continue;
                }

                onUpdateParamsList.Add($"{modifier}{p.TypeName} {p.Name}");
                onUpdateCallArgsList.Add($"{modifier}{p.Name}");

                if (IsStateType(p.TypeName))
                {
                }
                else if (IsWorldType(p.TypeName))
                {
                }
                else
                {
                    systemParamFields.Add((p.Name, p.TypeName));
                    if (isEventsDriven && i == eventsParamIndex)
                    {
                        systemParamUpdateBatchedList.Add(
                            $"                Range _range_{p.Name} = new Range(0, {p.Name}.Count);");
                        systemParamUpdateBatchedList.Add(
                            $"                {p.Name}.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref _range_{p.Name}));");
                        systemParamUpdateParallelList.Add(
                            $"                var _copy_{p.Name} = {p.Name};");
                        systemParamUpdateParallelList.Add(
                            $"                _copy_{p.Name}.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
                    }
                    else
                    {
                        systemParamUpdateBatchedList.Add(
                            $"                {p.Name}.Update(ref {stateParamName}.World, IntPtr.Zero);");
                        systemParamUpdateParallelList.Add(
                            $"                {p.Name}.Update(ref {stateParamName}.World, IntPtr.Zero);");
                    }
                }
            }

            var onUpdateParams = string.Join(", ", onUpdateParamsList);
            var onUpdateCallArgs = string.Join(", ", onUpdateCallArgsList);

            var onUpdateBatchedParams = onUpdateParams;
            if (!hasStateParam)
                onUpdateBatchedParams = onUpdateParams.Length > 0
                    ? onUpdateParams + ", ref State state"
                    : "ref State state";

            var onUpdateBatchedCallArgs = onUpdateCallArgs;
            if (!hasStateParam)
                onUpdateBatchedCallArgs = onUpdateCallArgs.Length > 0
                    ? onUpdateCallArgs + ", ref state"
                    : "ref state";

            var onUpdateBatchedParallelParams = onUpdateBatchedParams.Length > 0
                ? onUpdateBatchedParams + ", Range range"
                : "Range range";

            var parallelCallArgs = hasQuery
                ? onUpdateCallArgs.Replace($"ref {queryParamName}", "ref _copy")
                : onUpdateCallArgs;

            var parallelCallArgsEventsList = new List<string>(onUpdateCallArgsList);
            if (isEventsDriven)
            {
                var evIdx = onUpdateCallArgsList.IndexOf(eventsParamName);
                if (evIdx >= 0)
                    parallelCallArgsEventsList[evIdx] = $"_copy_{eventsParamName}";
            }
            var parallelCallArgsEvents = string.Join(", ", parallelCallArgsEventsList);

            var systemParamUpdateBatched = systemParamUpdateBatchedList.Count > 0
                ? string.Join("\n", systemParamUpdateBatchedList) + "\n"
                : "";

            var systemParamUpdateParallel = systemParamUpdateParallelList.Count > 0
                ? string.Join("\n", systemParamUpdateParallelList) + "\n"
                : "";

            sb.AppendLine($"    public struct {jobName} {{");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public void OnUpdate({onUpdateParams}) {{");
            sb.AppendLine($"            {declaringTypeFullName}.{methodName}({onUpdateCallArgs});");
            sb.AppendLine("        }");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public unsafe void OnUpdateBatched({onUpdateBatchedParams}) {{");
            if (hasQuery)
            {
                sb.AppendLine($"                Range range = new Range(0, {queryParamName}.Count);");
                sb.AppendLine($"                {queryParamName}.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
                sb.Append(systemParamUpdateBatched);
                sb.AppendLine($"                OnUpdate({onUpdateCallArgs});");
            }
            else
            {
                sb.Append(systemParamUpdateBatched);
                sb.AppendLine($"                OnUpdate({onUpdateCallArgs});");
            }
            sb.AppendLine("        }");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public unsafe void OnUpdateBatchedParallel({onUpdateBatchedParallelParams}) {{");
            if (hasQuery)
            {
                sb.AppendLine($"                var _copy = {queryParamName};");
                sb.AppendLine($"                _copy.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
                sb.Append(systemParamUpdateBatched);
                sb.AppendLine($"                OnUpdate({parallelCallArgs});");
            }
            else if (isEventsDriven)
            {
                sb.Append(systemParamUpdateParallel);
                sb.AppendLine($"                OnUpdate({parallelCallArgsEvents});");
            }
            else
            {
                sb.Append(systemParamUpdateBatched);
                sb.AppendLine($"                OnUpdateBatched({onUpdateBatchedCallArgs});");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine($"    public unsafe class {runnerName} : ISystemRunner, Systems.ISystemWithDeserialization {{");
            sb.AppendLine($"        public {jobName} System;");
            if (hasQuery)
            {
                sb.AppendLine($"        public ptr<{queryType}> Query;");
            }
            foreach (var sp in systemParamFields)
                sb.AppendLine($"        public ptr<{sp.type}> {sp.name};");
            sb.AppendLine("        public Threads Mode;");
            sb.AppendLine("        public ECBJob EcbJob;");
            sb.AppendLine($"        public string Name => typeof({jobName}).Name;");
            sb.AppendLine();
            sb.AppendLine("        public JobHandle Schedule(UpdateContext updateContext, ref State state) {");
            sb.AppendLine("            ref var world = ref state.World;");
            sb.AppendLine("            if (Mode == Threads.Main) {");

            var batchedCallArgsList = new List<string>();
            if (hasQuery) batchedCallArgsList.Add("ref Query.Ref");
            for (int i = 0; i < sourceParams.Length; i++)
            {
                var p = sourceParams[i];
                if (i == queryParamIndex) continue;
                if (IsStateType(p.TypeName)) { batchedCallArgsList.Add("ref state"); continue; }
                if (IsWorldType(p.TypeName)) { batchedCallArgsList.Add("ref state.World"); continue; }
                batchedCallArgsList.Add($"ref {p.Name}.Ref");
            }
            if (!hasStateParam) batchedCallArgsList.Add("ref state");
            var batchedCallArgs = string.Join(", ", batchedCallArgsList);

            sb.AppendLine($"                System.OnUpdateBatched({batchedCallArgs});");
            sb.AppendLine("                if (world.UnsafeWorld->ECB.HasCommands) {");
            sb.AppendLine("                    EcbJob.ECB = world.UnsafeWorld->ECB;");
            sb.AppendLine("                    EcbJob.world = world;");
            sb.AppendLine("                    EcbJob.Execute();");
            sb.AppendLine("                }");
            sb.AppendLine("            } else {");
            if (hasQuery)
            {
                sb.AppendLine("                Range range = new Range(0, Query.Ref.Count);");
                sb.AppendLine("                Query.Ref.Update(ref state.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
            }
            else if (isEventsDriven)
            {
                sb.AppendLine($"                Range _range_{eventsParamName} = new Range(0, {eventsParamName}.Ref.Count);");
                sb.AppendLine($"                {eventsParamName}.Ref.Update(ref state.World, (IntPtr)UnsafeUtility.AddressOf(ref _range_{eventsParamName}));");
            }

            var runCallArgsList = new List<string>();
            if (hasQuery) runCallArgsList.Add("ref Query.Ref");
            for (int i = 0; i < sourceParams.Length; i++)
            {
                var p = sourceParams[i];
                if (i == queryParamIndex) continue;
                if (IsStateType(p.TypeName)) { runCallArgsList.Add("ref state"); continue; }
                if (IsWorldType(p.TypeName)) { runCallArgsList.Add("ref state.World"); continue; }
                runCallArgsList.Add($"ref {p.Name}.Ref");
            }
            var runCallArgs = string.Join(", ", runCallArgsList);

            sb.AppendLine($"                System.OnUpdate({runCallArgs});");
            sb.AppendLine("                EcbJob.ECB = world.UnsafeWorld->ECB;");
            sb.AppendLine("                EcbJob.world = world;");
            sb.AppendLine("                EcbJob.Run();");
            sb.AppendLine("            }");
            sb.AppendLine("            return state.Dependencies;");
            sb.AppendLine("        }");
            sb.AppendLine("        public void Run(ref State state) {");
            if (hasQuery)
            {
                sb.AppendLine("            Range range = new Range(0, Query.Ref.Count);");
                sb.AppendLine("            Query.Ref.Update(ref state.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
            }
            else if (isEventsDriven)
            {
                sb.AppendLine($"            Range _range_{eventsParamName} = new Range(0, {eventsParamName}.Ref.Count);");
                sb.AppendLine($"            {eventsParamName}.Ref.Update(ref state.World, (IntPtr)UnsafeUtility.AddressOf(ref _range_{eventsParamName}));");
            }
            sb.AppendLine($"            System.OnUpdate({runCallArgs});");
            sb.AppendLine("            state.World.UnsafeWorld->ECB.Playback(ref state.World);");
            sb.AppendLine("        }");
            sb.AppendLine("        public void OnWorldDeserialize(World world) {");
            if (hasQuery)
            {
                sb.AppendLine("            var allocator = world.AllocatorRef;");
                sb.AppendLine("            Query.OnDeserialize(ref allocator);");
                sb.AppendLine("            Query.Ref.FixPointers(ref allocator);");
            }
            foreach (var sp in systemParamFields)
                sb.AppendLine($"            {sp.name} = world.UnsafeWorld->GetSystemParam2<{sp.type}>();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            var runnerFieldInits = "";
            if (hasQuery)
                runnerFieldInits += $"\n                    Query = world->GetSystemParam2<{queryType}>(),";
            foreach (var sp in systemParamFields)
                runnerFieldInits += $"\n                    {sp.name} = world->GetSystemParam2<{sp.type}>(),";

            sb.AppendLine($"    public static unsafe class {factoryName} {{");
            sb.AppendLine("        public static ISystemRunner CreateRunner(IntPtr worldPtr, Threads mode) {");
            sb.AppendLine("            var world = (World.WorldUnsafe*)worldPtr.ToPointer();");
            sb.AppendLine($"            var runner = new {runnerName}() {{");
            sb.AppendLine($"                System = new {jobName}(), Mode = mode, EcbJob = default,{runnerFieldInits}");
            sb.AppendLine("            };");
            sb.AppendLine("            return runner;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void GenerateLegacyRunner(
            StringBuilder sb, MethodInfo method,
            string jobName, string generatedName, string factoryName,
            string declaringTypeFullName, string methodName, int compileCount)
        {
            var parameters = method.GetParameters();
            var interfaceName = $"I{generatedName}SystemJob";
            var runnerName = $"I{generatedName}QuerySystemJobRunner";

            var queryParamIndex = -1;
            for (int i = 0; i < parameters.Length; i++)
            {
                var elemType = parameters[i].ParameterType.IsByRef
                    ? parameters[i].ParameterType.GetElementType()!
                    : parameters[i].ParameterType;
                var checkType = elemType;
                while (checkType != null && !checkType.Name.StartsWith("Query"))
                    checkType = checkType.DeclaringType;
                if (checkType != null)
                {
                    queryParamIndex = i;
                    break;
                }
            }

            var hasQuery = queryParamIndex >= 0;
            var queryType = hasQuery
                ? GetFullTypeName(parameters[queryParamIndex].ParameterType.GetElementType()!)
                : null;
            var queryParamName = hasQuery ? parameters[queryParamIndex].Name! : null;

            var eventsParamIndex = -1;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i == queryParamIndex) continue;
                var elemType = parameters[i].ParameterType.IsByRef
                    ? parameters[i].ParameterType.GetElementType()!
                    : parameters[i].ParameterType;
                var elemTypeName = GetFullTypeName(elemType);
                if (IsEventsType(elemTypeName))
                {
                    eventsParamIndex = i;
                    break;
                }
            }
            var hasEvents = eventsParamIndex >= 0;
            var isEventsDriven = hasEvents && !hasQuery;
            var eventsParamName = hasEvents
                ? parameters[eventsParamIndex].Name!
                : "";

            var hasStateParam = false;
            var stateParamName = "state";
            for (int i = 0; i < parameters.Length; i++)
            {
                var elemType = parameters[i].ParameterType.IsByRef
                    ? parameters[i].ParameterType.GetElementType()!
                    : parameters[i].ParameterType;
                if (elemType.Name == "State")
                {
                    hasStateParam = true;
                    stateParamName = parameters[i].Name!;
                    break;
                }
            }

            var onUpdateParamsList = new List<string>();
            var onUpdateCallArgsList = new List<string>();
            var systemParamFields = new List<(string name, string type)>();
            var systemParamUpdateBatchedList = new List<string>();
            var systemParamUpdateParallelList = new List<string>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var isByRef = param.ParameterType.IsByRef;
                var elemType = isByRef ? param.ParameterType.GetElementType()! : param.ParameterType;
                var pName = param.Name!;
                var pTypeName = GetFullTypeName(elemType);
                var modifier = isByRef ? "ref " : "";

                if (i == queryParamIndex)
                {
                    onUpdateParamsList.Add($"ref {pTypeName} {pName}");
                    onUpdateCallArgsList.Add($"ref {pName}");
                    continue;
                }

                onUpdateParamsList.Add($"{modifier}{pTypeName} {pName}");
                onUpdateCallArgsList.Add($"{modifier}{pName}");

                if (elemType.Name == "State")
                {
                }
                else if (elemType.Name == "World" || elemType.Name == "WorldUnsafe")
                {
                }
                else
                {
                    var spName = pName;
                    var spType = pTypeName;
                    systemParamFields.Add((spName, spType));
                    if (isEventsDriven && i == eventsParamIndex)
                    {
                        systemParamUpdateBatchedList.Add(
                            $"                Range _range_{spName} = new Range(0, {spName}.Count);");
                        systemParamUpdateBatchedList.Add(
                            $"                {spName}.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref _range_{spName}));");
                        systemParamUpdateParallelList.Add(
                            $"                var _copy_{spName} = {spName};");
                        systemParamUpdateParallelList.Add(
                            $"                _copy_{spName}.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
                    }
                    else
                    {
                        systemParamUpdateBatchedList.Add(
                            $"                {spName}.Update(ref {stateParamName}.World, IntPtr.Zero);");
                        systemParamUpdateParallelList.Add(
                            $"                {spName}.Update(ref {stateParamName}.World, IntPtr.Zero);");
                    }
                }
            }

            var onUpdateParams = string.Join(", ", onUpdateParamsList);
            var onUpdateCallArgs = string.Join(", ", onUpdateCallArgsList);

            var onUpdateBatchedParams = onUpdateParams;
            if (!hasStateParam)
                onUpdateBatchedParams = onUpdateParams.Length > 0
                    ? onUpdateParams + ", ref State state"
                    : "ref State state";

            var onUpdateBatchedCallArgs = onUpdateCallArgs;
            if (!hasStateParam)
                onUpdateBatchedCallArgs = onUpdateCallArgs.Length > 0
                    ? onUpdateCallArgs + ", ref state"
                    : "ref state";

            var onUpdateBatchedParallelParams = onUpdateBatchedParams.Length > 0
                ? onUpdateBatchedParams + ", Range range"
                : "Range range";

            var parallelCallArgs = hasQuery
                ? onUpdateCallArgs.Replace($"ref {queryParamName}", "ref _copy")
                : onUpdateCallArgs;

            var parallelCallArgsEventsList = new List<string>(onUpdateCallArgsList);
            if (isEventsDriven)
            {
                var evIdx = onUpdateCallArgsList.IndexOf(eventsParamName);
                if (evIdx >= 0)
                    parallelCallArgsEventsList[evIdx] = $"_copy_{eventsParamName}";
            }
            var parallelCallArgsEvents = string.Join(", ", parallelCallArgsEventsList);

            var systemParamUpdateBatched = systemParamUpdateBatchedList.Count > 0
                ? string.Join("\n", systemParamUpdateBatchedList) + "\n"
                : "";

            var systemParamUpdateParallel = systemParamUpdateParallelList.Count > 0
                ? string.Join("\n", systemParamUpdateParallelList) + "\n"
                : "";

            sb.AppendLine($"    public struct {jobName} : {interfaceName} {{");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public void OnUpdate({onUpdateParams}) {{");
            sb.AppendLine($"            {declaringTypeFullName}.{methodName}({onUpdateCallArgs});");
            sb.AppendLine("        }");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public unsafe void OnUpdateBatched({onUpdateBatchedParams}) {{");
            if (hasQuery)
            {
                sb.AppendLine($"                Range range = new Range(0, {queryParamName}.Count);");
                sb.AppendLine($"                {queryParamName}.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
                sb.Append(systemParamUpdateBatched);
                sb.AppendLine($"                OnUpdate({onUpdateCallArgs});");
            }
            else
            {
                sb.Append(systemParamUpdateBatched);
                sb.AppendLine($"                OnUpdate({onUpdateCallArgs});");
            }
            sb.AppendLine("        }");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public unsafe void OnUpdateBatchedParallel({onUpdateBatchedParallelParams}) {{");
            if (hasQuery)
            {
                sb.AppendLine($"                var _copy = {queryParamName};");
                sb.AppendLine($"                _copy.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
                sb.Append(systemParamUpdateBatched);
                sb.AppendLine($"                OnUpdate({parallelCallArgs});");
            }
            else if (isEventsDriven)
            {
                sb.Append(systemParamUpdateParallel);
                sb.AppendLine($"                OnUpdate({parallelCallArgsEvents});");
            }
            else
            {
                sb.Append(systemParamUpdateBatched);
                sb.AppendLine($"                OnUpdateBatched({onUpdateBatchedCallArgs});");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            var runnerFieldInits = "";
            if (hasQuery)
                runnerFieldInits += $"\n                    Query = world->GetSystemParam2<{queryType}>(),";
            foreach (var sp in systemParamFields)
                runnerFieldInits += $"\n                    {sp.name} = world->GetSystemParam2<{sp.type}>(),";

            sb.AppendLine($"    public static unsafe class {factoryName} {{");
            sb.AppendLine("        public static ISystemRunner CreateRunner(IntPtr worldPtr, Threads mode) {");
            sb.AppendLine("            var world = (World.WorldUnsafe*)worldPtr.ToPointer();");
            sb.AppendLine($"            var runner = new {runnerName}<{jobName}>() {{");
            sb.AppendLine($"                System = new {jobName}(), Mode = mode, EcbJob = default,{runnerFieldInits}");
            sb.AppendLine("            };");
            sb.AppendLine("            return runner;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static List<string> CollectReferences()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (_cachedReferences != null && _cachedReferencesAssemblyCount == assemblies.Length)
                return _cachedReferences;

            var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in assemblies)
            {
                try
                {
                    if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
                        refs.Add(asm.Location);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            _cachedReferences = refs.ToList();
            _cachedReferencesAssemblyCount = assemblies.Length;
            return _cachedReferences;
        }

        private static string BuildArgs(string csc, string output, string[] sources, List<string> references)
        {
            var sb = new System.Text.StringBuilder();

            if (_isDotnet && _cscDllPath != null)
                sb.Append($"exec \"{_cscDllPath}\" ");

            sb.Append("-target:library -nologo -unsafe -langversion:latest ");
            sb.Append($"-out:\"{output}\" ");

            foreach (var r in references)
                sb.Append($"-r:\"{r}\" ");

            foreach (var s in sources)
                sb.Append($"\"{s}\" ");

            return sb.ToString();
        }

        private static (int exitCode, string stderr, string stdout) RunProcessBackground(string exe, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            var stderrTask = process!.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            process.WaitForExit(30000);

            return (process.ExitCode, stderrTask.Result, stdoutTask.Result);
        }

        private static Func<IntPtr, Threads, ISystemRunner>[] CreateFactories(Assembly assembly, MethodInfo[] methods, int compileCount)
        {
            var factories = new Func<IntPtr, Threads, ISystemRunner>[methods.Length];

            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                var generatedName = $"{method.DeclaringType?.Name ?? ""}_{method.Name}";
                var factoryTypeName = $"Wargon.Nukecs.HotReload.Wrappers.{generatedName}_HR_Factory_{compileCount}";
                var factoryType = assembly.GetType(factoryTypeName);
                if (factoryType == null)
                {
                    Debug.LogError($"[HotReload] Factory type not found: {factoryTypeName}");
                    return null;
                }

                var createMethod = factoryType.GetMethod("CreateRunner",
                    BindingFlags.Public | BindingFlags.Static);
                if (createMethod == null)
                {
                    Debug.LogError($"[HotReload] CreateRunner method not found in {factoryTypeName}");
                    return null;
                }

                factories[i] = (Func<IntPtr, Threads, ISystemRunner>)Delegate.CreateDelegate(
                    typeof(Func<IntPtr, Threads, ISystemRunner>), createMethod);
            }

            return factories;
        }

        private static string GetFullTypeName(Type type)
        {
            if (!type.IsGenericType)
                return (type.FullName ?? type.Name).Replace('+', '.');

            var openName = type.GetGenericTypeDefinition().FullName;
            if (openName == null)
                return type.Name;

            var parts = openName.Split('+');
            var allArgs = type.GetGenericArguments();
            var result = new StringBuilder();
            var argOffset = 0;

            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0) result.Append('.');
                var part = parts[i];
                var btIdx = part.IndexOf('`');
                if (btIdx >= 0)
                {
                    var arity = int.Parse(part.Substring(btIdx + 1));
                    result.Append(part.Substring(0, btIdx));
                    if (argOffset + arity <= allArgs.Length)
                    {
                        result.Append('<');
                        for (int j = 0; j < arity; j++)
                        {
                            if (j > 0) result.Append(", ");
                            result.Append(GetFullTypeName(allArgs[argOffset + j]));
                        }
                        result.Append('>');
                        argOffset += arity;
                    }
                }
                else
                {
                    result.Append(part);
                }
            }

            return result.ToString();
        }

        private struct SourceMethodParam
        {
            public string TypeName;
            public string Name;
            public bool IsByRef;
        }

        private static SourceMethodParam[] ParseMethodParamsFromSource(string sourceCode, string methodName)
        {
            var searchPos = 0;
            while (true)
            {
                var idx = sourceCode.IndexOf("void " + methodName, searchPos, StringComparison.Ordinal);
                if (idx < 0)
                {
                    idx = sourceCode.IndexOf("void\t" + methodName, searchPos, StringComparison.Ordinal);
                    if (idx < 0) return null;
                }

                var afterName = idx + 5 + methodName.Length;
                while (afterName < sourceCode.Length && char.IsWhiteSpace(sourceCode[afterName]))
                    afterName++;

                if (afterName < sourceCode.Length && sourceCode[afterName] == '(')
                {
                    var parenStart = afterName;
                    var parenEnd = FindMatchingParen(sourceCode, parenStart);
                    if (parenEnd < 0) return null;

                    var paramText = sourceCode.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
                    if (string.IsNullOrEmpty(paramText)) return Array.Empty<SourceMethodParam>();

                    paramText = System.Text.RegularExpressions.Regex.Replace(paramText, @"\s+", " ").Trim();
                    return ParseParameterList(paramText);
                }

                searchPos = idx + 1;
            }
        }

        private static int FindMatchingParen(string text, int openIdx)
        {
            int depth = 1;
            for (int i = openIdx + 1; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static SourceMethodParam[] ParseParameterList(string paramText)
        {
            var parameters = new List<SourceMethodParam>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < paramText.Length; i++)
            {
                if (paramText[i] == '<') depth++;
                else if (paramText[i] == '>') depth--;
                else if (paramText[i] == '[')
                {
                    var closeBracket = paramText.IndexOf(']', i);
                    if (closeBracket >= 0) i = closeBracket;
                }
                else if (paramText[i] == ',' && depth == 0)
                {
                    var param = ParseSingleParam(paramText.Substring(start, i - start).Trim());
                    if (param != null) parameters.Add(param.Value);
                    start = i + 1;
                }
            }

            if (start < paramText.Length)
            {
                var param = ParseSingleParam(paramText.Substring(start).Trim());
                if (param != null) parameters.Add(param.Value);
            }

            return parameters.ToArray();
        }

        private static SourceMethodParam? ParseSingleParam(string paramText)
        {
            if (string.IsNullOrEmpty(paramText)) return null;

            paramText = System.Text.RegularExpressions.Regex.Replace(paramText, @"\[[^\]]*\]", "").Trim();
            paramText = System.Text.RegularExpressions.Regex.Replace(paramText, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(paramText)) return null;

            var parts = new List<string>();
            int gDepth = 0;
            var current = new StringBuilder();
            for (int i = 0; i < paramText.Length; i++)
            {
                var ch = paramText[i];
                if (ch == '<') { gDepth++; current.Append(ch); }
                else if (ch == '>') { gDepth--; current.Append(ch); }
                else if (ch == ' ' && gDepth == 0)
                {
                    if (current.Length > 0) { parts.Add(current.ToString()); current.Clear(); }
                }
                else { current.Append(ch); }
            }
            if (current.Length > 0) parts.Add(current.ToString());

            if (parts.Count < 2) return null;

            var name = parts[parts.Count - 1];
            var firstPart = parts[0];
            var isByRef = firstPart == "ref" || firstPart == "out" || firstPart == "in";

            var typeStartIdx = isByRef ? 1 : 0;
            if (typeStartIdx >= parts.Count - 1) return null;

            var typeParts = new List<string>();
            for (int i = typeStartIdx; i < parts.Count - 1; i++)
                typeParts.Add(parts[i]);

            var typeName = string.Join(" ", typeParts);

            return new SourceMethodParam
            {
                TypeName = typeName,
                Name = name,
                IsByRef = isByRef
            };
        }

        private static bool IsQueryType(string typeName)
        {
            var shortName = typeName.Contains('.') ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;
            return shortName.StartsWith("Query<") || shortName == "Query";
        }

        private static bool IsStateType(string typeName)
        {
            var shortName = typeName.Contains('.') ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;
            return shortName == "State";
        }

        private static bool IsEventsType(string typeName)
        {
            var shortName = typeName.Contains('.') ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;
            return shortName.StartsWith("Events<");
        }

        private static bool IsWorldType(string typeName)
        {
            var shortName = typeName.Contains('.') ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;
            return shortName == "World" || shortName == "WorldUnsafe";
        }
    }
}
#endif