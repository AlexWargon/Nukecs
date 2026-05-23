using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
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

        public static event Action<string, MethodInfo[], Func<IntPtr, Threads, IntPtr, ISystemRunner>[]> OnSystemsCompiled;

        public static void PrewarmCache()
        {
            EnsureSystemMethodCache();
            FindCsc();
            CollectReferences();
            HotReloadRoslynCompiler.Initialize();
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

            var wrapperSource = GenerateJobAndFactory(methods, usings, count);
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
                    string compilerUsed = "roslyn";
                    long compileMs;

                    if (roslynAvailable)
                    {
                        HotReloadRoslynCompiler.BuildMetadataReferences(references);
                        dllBytes = HotReloadRoslynCompiler.Compile(wrapperSource, sourceCode, $"HotReload_{count}");
                    }

                    compileMs = sw.ElapsedMilliseconds;

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
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        {
                            if (method.GetCustomAttributes(typeof(SystemAttribute), false).Length > 0)
                            {
                                var key = $"{type.Name}.{method.Name}";
                                if (!_systemMethodCache.ContainsKey(key))
                                    _systemMethodCache[key] = method;
                            }
                        }
                    }
                }
                catch { }
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
                if (sourceCode.Contains(method.Name) && sourceCode.Contains(method.DeclaringType.Name) && seen.Add(kv.Key))
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

        private static string GenerateJobAndFactory(MethodInfo[] methods, string[] fileUsings, int compileCount)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using Unity.Collections.LowLevel.Unsafe;");
            sb.AppendLine("using Unity.Jobs;");
            sb.AppendLine("using Unity.Jobs.LowLevel.Unsafe;");
            sb.AppendLine("using Wargon.Nukecs;");
            foreach (var u in fileUsings)
            {
                if (u != "using System;" && !u.Contains("Runtime.CompilerServices") &&
                    !u.Contains("using Wargon.Nukecs;"))
                    sb.AppendLine(u);
            }
            sb.AppendLine();

            sb.AppendLine("namespace Wargon.Nukecs.HotReload.Wrappers");
            sb.AppendLine("{");

            for (int mi = 0; mi < methods.Length; mi++)
            {
                var method = methods[mi];
                var parameters = method.GetParameters();
                var containingTypeName = method.DeclaringType?.Name ?? "";
                var methodName = method.Name;
                var generatedName = $"{containingTypeName}_{methodName}";
                var interfaceName = $"I{generatedName}SystemJob";
                var runnerName = $"I{generatedName}QuerySystemJobRunner";
                var jobName = $"{generatedName}_HR_Job_{compileCount}";
                var factoryName = $"{generatedName}_HR_Factory_{compileCount}";
                var declaringTypeFullName = GetFullTypeName(method.DeclaringType!);

                var queryParamIndex = -1;
                for (int i = 0; i < parameters.Length; i++)
                {
                    var elemType = parameters[i].ParameterType.IsByRef
                        ? parameters[i].ParameterType.GetElementType()!
                        : parameters[i].ParameterType;
                    if (elemType.Name.StartsWith("Query"))
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
                        systemParamUpdateBatchedList.Add(
                            $"                {spName}.Update(ref {stateParamName}.World, IntPtr.Zero);");
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

                var systemParamUpdateBatched = systemParamUpdateBatchedList.Count > 0
                    ? string.Join("\n", systemParamUpdateBatchedList) + "\n"
                    : "";

                sb.AppendLine($"    public struct {jobName} : {interfaceName} {{");
                sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine($"        public void OnUpdate({onUpdateParams}) {{");
                sb.AppendLine(
                    $"            {declaringTypeFullName}.{methodName}({onUpdateCallArgs});");
                sb.AppendLine("        }");
                sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine($"        public unsafe void OnUpdateBatched({onUpdateBatchedParams}) {{");
                if (hasQuery)
                {
                    sb.AppendLine(
                        $"                Range range = new Range(0, {queryParamName}.Count);");
                    sb.AppendLine(
                        $"                {queryParamName}.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
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
                sb.AppendLine(
                    $"        public unsafe void OnUpdateBatchedParallel({onUpdateBatchedParallelParams}) {{");
                if (hasQuery)
                {
                    sb.AppendLine($"                var _copy = {queryParamName};");
                    sb.AppendLine(
                        $"                _copy.Update(ref {stateParamName}.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
                    sb.Append(systemParamUpdateBatched);
                    sb.AppendLine($"                OnUpdate({parallelCallArgs});");
                }
                else
                {
                    sb.Append(systemParamUpdateBatched);
                    sb.AppendLine($"                OnUpdateBatched({onUpdateBatchedCallArgs});");
                }

                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();

                sb.AppendLine($"    public static unsafe class {factoryName} {{");
                sb.AppendLine(
                    "        public static ISystemRunner CreateRunner(IntPtr worldPtr, Threads mode, IntPtr existingQueryPtr) {");
                sb.AppendLine(
                    "            var world = (World.WorldUnsafe*)worldPtr.ToPointer();");

                var queryFieldInit = "";
                if (hasQuery)
                {
                    sb.AppendLine(
                        $"            var _existingQuery = new ptr<{queryType}>((byte*)existingQueryPtr.ToPointer(), 0, true);");
                    queryFieldInit = $"\n                    Query = _existingQuery,";
                }

                var systemParamFieldInits = "";
                foreach (var sp in systemParamFields)
                    systemParamFieldInits += $"\n                    {sp.name} = world->GetSystemParam2<{sp.type}>(),";

                sb.AppendLine(
                    $"                var runner = new {runnerName}<{jobName}>() {{");
                sb.AppendLine(
                    $"                    System = new {jobName}(), Mode = mode, EcbJob = default,{queryFieldInit}{systemParamFieldInits}");
                sb.AppendLine("                };");

                sb.AppendLine("                return runner;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            return sb.ToString();
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
                catch { }
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
            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            process.WaitForExit(30000);

            return (process.ExitCode, stderrTask.Result, stdoutTask.Result);
        }

        private static (int exitCode, string stderr) RunProcess(string exe, string args)
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
            var stderr = process.StandardError.ReadToEnd();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);

            if (!string.IsNullOrEmpty(stdout))
                Debug.Log($"[HotReload] csc output:\n{stdout}");

            return (process.ExitCode, stderr);
        }

        private static Func<IntPtr, Threads, IntPtr, ISystemRunner>[] CreateFactories(Assembly assembly, MethodInfo[] methods, int compileCount)
        {
            var factories = new Func<IntPtr, Threads, IntPtr, ISystemRunner>[methods.Length];

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

                factories[i] = (Func<IntPtr, Threads, IntPtr, ISystemRunner>)Delegate.CreateDelegate(
                    typeof(Func<IntPtr, Threads, IntPtr, ISystemRunner>), createMethod);
            }

            return factories;
        }

        private static string GetFullTypeName(Type type)
        {
            if (!type.IsGenericType)
                return (type.FullName ?? type.Name).Replace('+', '.');

            var genericDef = type.GetGenericTypeDefinition().FullName.Replace('+', '.');
            var backtickIdx = genericDef.IndexOf('`');
            if (backtickIdx >= 0)
                genericDef = genericDef.Substring(0, backtickIdx);

            var args = string.Join(", ", type.GetGenericArguments().Select(GetFullTypeName));
            return $"{genericDef}<{args}>";
        }
    }
}
