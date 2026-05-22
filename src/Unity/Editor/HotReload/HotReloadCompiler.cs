using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static int _compileCount;
        private static Dictionary<string, MethodInfo> _systemMethodCache;
        private static List<string> _cachedReferences;
        private static int _cachedReferencesAssemblyCount;

        public static event Action<string, MethodInfo[], Action<IntPtr, IntPtr, IntPtr>[]> OnSystemsCompiled;

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
            var csc = FindCsc();
            if (csc == null)
            {
                Debug.LogError("[HotReload] csc.exe not found");
                return;
            }

            var sourceCode = File.ReadAllText(sourceFilePath);
            var methods = FindSystemMethods(sourceCode);
            if (methods == null || methods.Length == 0)
            {
                Debug.Log($"[HotReload] No [System] methods found in {Path.GetFileName(sourceFilePath)}");
                return;
            }

            var usings = ExtractUsings(sourceCode);

            _tempDir = Path.Combine(Path.GetTempPath(), "NukecsHotReload");
            if (!Directory.Exists(_tempDir))
                Directory.CreateDirectory(_tempDir);

            _compileCount++;

            var wrapperSource = GenerateWrapper(sourceFilePath, methods, usings);
            var wrapperFile = Path.Combine(_tempDir, $"Wrapper_{_compileCount}.cs");
            var outputFile = Path.Combine(_tempDir, $"HotReload_{_compileCount}.dll");

            File.WriteAllText(wrapperFile, wrapperSource);

            var references = CollectReferences();
            var args = BuildArgs(csc, outputFile, new[] { wrapperFile, sourceFilePath }, references);
            var capturedCompileCount = _compileCount;

            Task.Run(() =>
            {
                var (exitCode, stderr, stdout) = RunProcessBackground(csc, args);
                if (exitCode != 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        Debug.LogError($"[HotReload] Compilation failed:\n{stderr}");
                    };
                    return;
                }

                var bytes = File.ReadAllBytes(outputFile);

                EditorApplication.delayCall += () =>
                {
                    var assembly = Assembly.Load(bytes);
                    var del = CreateDelegates(assembly, methods, capturedCompileCount);
                    if (del != null)
                    {
                        OnSystemsCompiled?.Invoke(sourceFilePath, methods, del);
                        Debug.Log($"[HotReload] Compiled {methods.Length} system(s) from {Path.GetFileName(sourceFilePath)}");
                    }
                };
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

        private static string GenerateWrapper(string sourceFilePath, MethodInfo[] methods, string[] fileUsings)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using Unity.Collections.LowLevel.Unsafe;");
            sb.AppendLine("using Wargon.Nukecs;");
            foreach (var u in fileUsings)
            {
                if (u != "using System;" && !u.Contains("Runtime.CompilerServices") && !u.Contains("using Wargon.Nukecs;"))
                    sb.AppendLine(u);
            }
            sb.AppendLine();

            sb.AppendLine("namespace Wargon.Nukecs.HotReload.Wrappers");
            sb.AppendLine("{");

            foreach (var method in methods)
            {
                var queryParams = method.GetParameters()
                    .Where(p => p.ParameterType.IsByRef && p.ParameterType.GetElementType()!.Name.StartsWith("Query"))
                    .ToArray();

                var stateParam = method.GetParameters()
                    .FirstOrDefault(p => p.ParameterType.IsByRef
                        && p.ParameterType.GetElementType()!.Name == "State");

                var hasQuery = queryParams.Length > 0;
                var hasState = stateParam != null;

                var queryType = hasQuery ? GetFullTypeName(queryParams[0].ParameterType.GetElementType()!) : null;
                var declaringTypeName = GetFullTypeName(method.DeclaringType!);
                var methodName = method.Name;

                sb.AppendLine($"    public static unsafe class {methodName}_Wrapper_{_compileCount}");
                sb.AppendLine("    {");

                sb.AppendLine("        public static void Execute(IntPtr unsafeWorldPtr, IntPtr statePtr, IntPtr queryRawPtr)");
                sb.AppendLine("        {");

                sb.AppendLine("            ref var state = ref Unsafe.AsRef<State>(statePtr.ToPointer());");

                if (hasQuery)
                {
                    sb.AppendLine($"            var query = default({queryType});");
                    sb.AppendLine("            if (queryRawPtr != IntPtr.Zero)");
                    sb.AppendLine("                query._query = new ptr<QueryUnsafe>((byte*)queryRawPtr, 0, true);");
                    sb.AppendLine("            if (query.Count > 0)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                Range range = new Range(0, query.Count);");
                    sb.AppendLine("                query.Update(ref state.World, (IntPtr)UnsafeUtility.AddressOf(ref range));");
                    sb.Append($"                {declaringTypeName}.{methodName}(ref query");
                    if (hasState) sb.Append(", ref state");
                    sb.AppendLine(");");
                    sb.AppendLine("            }");
                }
                else
                {
                    sb.Append($"            {declaringTypeName}.{methodName}(");
                    if (hasState) sb.Append("ref state");
                    sb.AppendLine(");");
                }

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
            var stderr = process.StandardError.ReadToEnd();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);

            return (process.ExitCode, stderr, stdout);
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

        private static Action<IntPtr, IntPtr, IntPtr>[] CreateDelegates(Assembly assembly, MethodInfo[] methods)
        {
            return CreateDelegates(assembly, methods, _compileCount);
        }

        private static Action<IntPtr, IntPtr, IntPtr>[] CreateDelegates(Assembly assembly, MethodInfo[] methods, int compileCount)
        {
            var delegates = new Action<IntPtr, IntPtr, IntPtr>[methods.Length];

            for (int i = 0; i < methods.Length; i++)
            {
                var wrapperTypeName = $"Wargon.Nukecs.HotReload.Wrappers.{methods[i].Name}_Wrapper_{compileCount}";
                var wrapperType = assembly.GetType(wrapperTypeName);
                if (wrapperType == null)
                {
                    Debug.LogError($"[HotReload] Wrapper type not found: {wrapperTypeName}");
                    return null;
                }

                var executeMethod = wrapperType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                if (executeMethod == null)
                {
                    Debug.LogError($"[HotReload] Execute method not found in {wrapperTypeName}");
                    return null;
                }

                delegates[i] = (Action<IntPtr, IntPtr, IntPtr>)Delegate.CreateDelegate(typeof(Action<IntPtr, IntPtr, IntPtr>), executeMethod);
            }

            return delegates;
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
