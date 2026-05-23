using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Wargon.Nukecs.HotReload
{
    public static class HotReloadRoslynCompiler
    {
        private static volatile bool _initialized;
        private static volatile bool _available;
        private static bool _compileAttempted;
        private static string _roslynDir;
        private static string _dotnetPath;
        private static string _serverDllPath;
        private static Process _serverProcess;
        private static BinaryWriter _writer;
        private static BinaryReader _reader;
        private static string _runtimeDir;
        private static Task<string> _stderrTask;

        private const int ServerTimeoutMs = 15000;
        private const int ServerCompileTimeoutMs = 60000;

        public static bool IsAvailable
        {
            get
            {
                if (!_initialized) Initialize();
                return _available;
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var editorPath = EditorApplication.applicationPath;
                var dataPath = editorPath.Replace(".exe", "") + "/Data";
                if (!Directory.Exists(dataPath))
                    dataPath = Path.Combine(Path.GetDirectoryName(editorPath)!, "Data");

                _roslynDir = Path.Combine(dataPath, "DotNetSdkRoslyn");
                var codeAnalysisDll = Path.Combine(_roslynDir, "Microsoft.CodeAnalysis.dll");
                var csharpDll = Path.Combine(_roslynDir, "Microsoft.CodeAnalysis.CSharp.dll");
                _dotnetPath = Path.Combine(dataPath, "NetCoreRuntime", "dotnet.exe");

                var netcoreDir = Path.Combine(dataPath, "NetCoreRuntime", "shared", "Microsoft.NETCore.App");
                if (Directory.Exists(netcoreDir))
                    _runtimeDir = Directory.GetDirectories(netcoreDir).OrderByDescending(d => d).FirstOrDefault();

                if (!File.Exists(codeAnalysisDll) || !File.Exists(csharpDll) || !File.Exists(_dotnetPath))
                    return;

                var tempDir = Path.Combine(Path.GetTempPath(), "NukecsHotReload");
                if (!Directory.Exists(tempDir))
                    Directory.CreateDirectory(tempDir);

                var sourceHash = ComputeHash(GetServerSource());
                _serverDllPath = Path.Combine(tempDir, $"NukecsRoslynServer_{sourceHash}.dll");

                if (File.Exists(_serverDllPath))
                {
                    _available = true;
                    _compileAttempted = true;
                }
                else
                {
                    _available = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HotReload-Roslyn] Init failed: {ex.Message}");
                _available = false;
            }
        }

        public static void BuildMetadataReferences(List<string> assemblyPaths)
        {
            if (!_available) return;

            try
            {
                if (!EnsureServerReady()) return;

                _writer.Write("REFS");
                _writer.Write(assemblyPaths.Count);
                foreach (var path in assemblyPaths)
                    _writer.Write(path);
                _writer.Flush();

                var response = ReadStringTimed();
                if (response != "OK")
                {
                    Debug.LogWarning($"[HotReload-Roslyn] Unexpected REFS response: {response}");
                    RestartServer();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HotReload-Roslyn] BuildMetadataReferences failed: {ex.Message}");
                RestartServer();
            }
        }

        public static byte[] Compile(string wrapperSource, string userSource, string assemblyName)
        {
            if (!_available) return null;

            try
            {
                if (!EnsureServerReady()) return null;

                _writer.Write("COMPILE");
                _writer.Write(assemblyName);
                _writer.Write(wrapperSource);
                _writer.Write(userSource);
                _writer.Flush();

                var result = ReadStringTimed();
                if (result == "OK")
                {
                    var length = ReadInt32Timed();
                    return ReadBytesTimed(length);
                }

                if (result == "ERROR")
                {
                    var errorMsg = ReadStringTimed();
                    Debug.LogError($"[HotReload-Roslyn] Errors:\n{errorMsg}");
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HotReload-Roslyn] Compile failed: {ex.Message}");
                RestartServer();
                return null;
            }
        }

        public static void Shutdown()
        {
            try
            {
                if (_writer != null)
                {
                    _writer.Write("EXIT");
                    _writer.Flush();
                }
            }
            catch { }

            KillServer();
        }

        private static string ReadStringTimed()
        {
            var task = Task.Run(() => _reader.ReadString());
            if (task.Wait(ServerTimeoutMs))
                return task.Result;
            RestartServer();
            throw new TimeoutException($"Server read timeout ({ServerTimeoutMs}ms)");
        }

        private static int ReadInt32Timed()
        {
            var task = Task.Run(() => _reader.ReadInt32());
            if (task.Wait(ServerTimeoutMs))
                return task.Result;
            RestartServer();
            throw new TimeoutException($"Server read timeout ({ServerTimeoutMs}ms)");
        }

        private static byte[] ReadBytesTimed(int count)
        {
            var task = Task.Run(() => _reader.ReadBytes(count));
            if (task.Wait(ServerTimeoutMs))
                return task.Result;
            RestartServer();
            throw new TimeoutException($"Server read timeout ({ServerTimeoutMs}ms)");
        }

        private static bool EnsureServerReady()
        {
            if (!_available) return false;

            try
            {
                if (!_compileAttempted && !File.Exists(_serverDllPath))
                {
                    _compileAttempted = true;
                    if (!CompileServerDll())
                    {
                        _available = false;
                        return false;
                    }
                }

                if (File.Exists(_serverDllPath))
                {
                    EnsureServerRunning();
                    return _writer != null && _serverProcess != null && !_serverProcess.HasExited;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

         private static void EnsureServerRunning()
         {
             if (_serverProcess != null && !_serverProcess.HasExited && _writer != null)
                 return;
 
             KillServer();
 
             WriteRuntimeConfig();
 
             var serverArgs = $"exec \"{_serverDllPath}\" \"{_roslynDir}\"";
             if (_runtimeDir != null)
                 serverArgs += $" \"{_runtimeDir}\"";
 
             var psi = new ProcessStartInfo
             {
                 FileName = _dotnetPath,
                 Arguments = serverArgs,
                 CreateNoWindow = true,
                 UseShellExecute = false,
                 RedirectStandardInput = true,
                 RedirectStandardOutput = true,
                 RedirectStandardError = true
             };
 
             _serverProcess = Process.Start(psi);
             _stderrTask = _serverProcess.StandardError.ReadToEndAsync();
             _writer = new BinaryWriter(_serverProcess.StandardInput.BaseStream);
             _reader = new BinaryReader(_serverProcess.StandardOutput.BaseStream);
 
             try
             {
                 var readyTask = Task.Run(() => _reader.ReadString());
                 if (!readyTask.Wait(ServerTimeoutMs))
                 {
                     LogHandshakeFailure("READY timeout");
                     return;
                 }
 
                 if (readyTask.IsFaulted || readyTask.IsCanceled)
                 {
                     var inner = readyTask.Exception?.InnerException?.Message ?? "unknown";
                     LogHandshakeFailure($"Server crashed before READY: {inner}");
                     return;
                 }
 
                 if (readyTask.Result != "READY")
                 {
                     LogHandshakeFailure($"Unexpected response: {readyTask.Result}");
                     return;
                 }
             }
             catch (Exception ex)
             {
                 LogHandshakeFailure($"Handshake exception: {ex.Message}");
             }
         }
 
         private static void LogHandshakeFailure(string reason)
         {
             if (_serverProcess != null && !_serverProcess.HasExited)
                 _serverProcess.WaitForExit(3000);
 
             var stderr = "";
             try
             {
                 if (_stderrTask != null && _stderrTask.Wait(2000))
                     stderr = _stderrTask.Result;
             } catch { }
 
             Debug.LogWarning($"[HotReload-Roslyn] {reason}{(string.IsNullOrEmpty(stderr) ? "" : $"\n  stderr: {stderr}")}");
             KillServer();
             _available = false;
         }
 
         private static void WriteRuntimeConfig()
         {
             try
             {
                 var configPath = Path.ChangeExtension(_serverDllPath, ".runtimeconfig.json");
                 if (File.Exists(configPath)) return;
 
                 var versionDir = _runtimeDir != null ? Path.GetFileName(_runtimeDir) : "6.0.0";
                 var json = $"{{\"runtimeOptions\":{{\"tfm\":\"net6.0\",\"framework\":{{\"name\":\"Microsoft.NETCore.App\",\"version\":\"{versionDir}\"}}}}}}";
                 File.WriteAllText(configPath, json);
             }
             catch { }
         }

        private static void RestartServer()
        {
            KillServer();
            _writer = null;
            _reader = null;
        }

        private static void KillServer()
        {
            if (_serverProcess == null) return;
            try
            {
                if (!_serverProcess.HasExited)
                    _serverProcess.Kill();
            }
            catch { }
            try { _serverProcess.Dispose(); } catch { }
            _serverProcess = null;
        }

        private static bool CompileServerDll()
        {
            var tempDir = Path.GetDirectoryName(_serverDllPath)!;
            var hash = ComputeHash(GetServerSource());
            var sourcePath = Path.Combine(tempDir, $"NukecsRoslynServer_{hash}.cs");
            File.WriteAllText(sourcePath, GetServerSource());

            var cscDllPath = Path.Combine(_roslynDir, "csc.dll");
            if (!File.Exists(cscDllPath))
            {
                Debug.LogWarning("[HotReload-Roslyn] csc.dll not found for server compilation");
                return false;
            }

            var refs = CollectServerReferences();
            if (TryCompileServerDll(cscDllPath, sourcePath, refs))
            {
                Debug.Log("[HotReload-Roslyn] Compilation server ready");
                return true;
            }

            return false;
        }

        private static bool TryCompileServerDll(string cscDllPath, string sourcePath, List<string> refs)
        {
            var args = new StringBuilder();
            args.Append($"exec \"{cscDllPath}\" ");
            args.Append("-target:exe -nologo -unsafe -langversion:latest ");
            args.Append($"-out:\"{_serverDllPath}\" ");
            foreach (var r in refs)
                args.Append($"-r:\"{r}\" ");
            args.Append($"\"{sourcePath}\"");

            var psi = new ProcessStartInfo
            {
                FileName = _dotnetPath,
                Arguments = args.ToString(),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();

            var exited = process.WaitForExit(ServerCompileTimeoutMs);
            if (!exited)
            {
                try { process.Kill(); } catch { }
                Debug.LogWarning($"[HotReload-Roslyn] Server compilation timed out ({ServerCompileTimeoutMs}ms)");
                return false;
            }

            var stderr = stderrTask.IsCompleted ? stderrTask.Result : "";
            var stdout = stdoutTask.IsCompleted ? stdoutTask.Result : "";
            var output = !string.IsNullOrEmpty(stderr) ? stderr : stdout;

            if (process.ExitCode != 0 || !File.Exists(_serverDllPath))
            {
                Debug.LogWarning($"[HotReload-Roslyn] Server compile exit={process.ExitCode}: {(string.IsNullOrEmpty(output) ? "(no output)" : output)}");
                return false;
            }

            return true;
        }

        private static List<string> CollectServerReferences()
        {
            var refs = new List<string>();

            if (Directory.Exists(_roslynDir))
            {
                foreach (var dll in Directory.GetFiles(_roslynDir, "*.dll"))
                    refs.Add(dll);
            }

            var dotnetDir = Path.GetDirectoryName(_dotnetPath);
            var netcoreDir = Path.Combine(dotnetDir!, "shared", "Microsoft.NETCore.App");
            if (Directory.Exists(netcoreDir))
            {
                var versionDir = Directory.GetDirectories(netcoreDir)
                    .OrderByDescending(d => d).FirstOrDefault();
                if (versionDir != null)
                {
                    foreach (var dll in Directory.GetFiles(versionDir, "*.dll"))
                    {
                        if (HasClrHeader(dll))
                            refs.Add(dll);
                    }
                }
            }

            return refs;
        }

        private static bool HasClrHeader(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);
                if (reader.ReadUInt16() != 0x5A4D) return false;
                stream.Position = 0x3C;
                var peOffset = reader.ReadInt32();
                stream.Position = peOffset;
                if (reader.ReadUInt32() != 0x4550) return false;
                reader.ReadUInt16();
                reader.ReadUInt16();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                var sizeOfOptionalHeader = reader.ReadUInt16();
                reader.ReadUInt16();
                var optionalHeaderStart = stream.Position;
                var magic = reader.ReadUInt16();
                int clrDirOffset;
                if (magic == 0x10B) clrDirOffset = 208;
                else if (magic == 0x20B) clrDirOffset = 224;
                else return false;
                if (clrDirOffset + 8 > sizeOfOptionalHeader) return false;
                stream.Position = optionalHeaderStart + clrDirOffset;
                return reader.ReadUInt32() != 0;
            }
            catch { return false; }
        }

        private static string ComputeHash(string source)
        {
            uint hash = 5381;
            foreach (var c in source)
                hash = ((hash << 5) + hash) ^ (uint)c;
            return hash.ToString("x8");
        }

        private static string GetServerSource()
        {
            return @"using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

class RoslynServer
{
    static List<MetadataReference> _refs;
    static string[] _searchDirs;

    static void Main(string[] args)
    {
        try
        {
            _searchDirs = args;
            AssemblyLoadContext.Default.Resolving += (ctx, name) =>
            {
                foreach (var dir in _searchDirs)
                {
                    var path = Path.Combine(dir, name.Name + "".dll"");
                    if (File.Exists(path))
                        return ctx.LoadFromAssemblyPath(path);
                }
                return null;
            };
            Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(""FATAL: "" + ex);
            Console.Error.Flush();
        }
    }

    static void Run()
    {
        var reader = new BinaryReader(Console.OpenStandardInput());
        var writer = new BinaryWriter(Console.OpenStandardOutput());

        writer.Write(""READY"");
        writer.Flush();

        while (true)
        {
            try
            {
                var cmd = reader.ReadString();
                switch (cmd)
                {
                    case ""REFS"": HandleRefs(reader, writer); break;
                    case ""COMPILE"": HandleCompile(reader, writer); break;
                    case ""EXIT"": return;
                }
            }
            catch (EndOfStreamException) { return; }
            catch (IOException) { return; }
            catch { return; }
        }
    }

    static void HandleRefs(BinaryReader reader, BinaryWriter writer)
    {
        var count = reader.ReadInt32();
        _refs = new List<MetadataReference>(count);
        for (int i = 0; i < count; i++)
        {
            try { _refs.Add(MetadataReference.CreateFromFile(reader.ReadString())); }
            catch { }
        }
        writer.Write(""OK"");
        writer.Flush();
    }

    static void HandleCompile(BinaryReader reader, BinaryWriter writer)
    {
        var assemblyName = reader.ReadString();
        var wrapperSrc = reader.ReadString();
        var userSrc = reader.ReadString();

        var wrapperTree = CSharpSyntaxTree.ParseText(wrapperSrc);
        var srcTree = CSharpSyntaxTree.ParseText(userSrc);

        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithAllowUnsafe(true);

        var compilation = CSharpCompilation.Create(assemblyName,
            new[] { wrapperTree, srcTree }, _refs, options);

        using (var peStream = new MemoryStream())
        {
            var result = compilation.Emit(peStream);

            if (result.Success)
            {
                var bytes = peStream.ToArray();
                writer.Write(""OK"");
                writer.Write(bytes.Length);
                writer.Write(bytes);
                writer.Flush();
            }
            else
            {
                var errors = new List<string>();
                foreach (var d in result.Diagnostics)
                {
                    if (d.Severity == DiagnosticSeverity.Error)
                        errors.Add(d.ToString());
                }
                writer.Write(""ERROR"");
                writer.Write(errors.Count > 0
                    ? string.Join(""\n"", errors)
                    : ""Unknown compilation error"");
                writer.Flush();
            }
        }
    }
}";
        }
    }
}
