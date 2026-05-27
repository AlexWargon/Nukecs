using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
#if UNITY_EDITOR
using UnityEngine;
using Wargon.Nukecs.HotReload;
#endif

namespace Wargon.Nukecs.HotReload
{
    public class HotReloadSystems : IDisposable
    {
        private struct SystemEntry
        {
            public string FilePath;
            public string MethodName;
            public string DeclaringTypeName;
            public Threads ThreadMode;
            public int RunnerIndex;
        }

        private Systems _systems;
        private bool _disposed;
        private readonly List<SystemEntry> _entries = new List<SystemEntry>();

        public Systems Systems => _systems;

        public HotReloadSystems(ref World world)
        {
            _systems = new Systems(ref world);
#if UNITY_EDITOR
            HotReloadCompiler.PrewarmCache();
            HotReloadCompiler.OnSystemsCompiled += OnSystemsCompiled;
#endif
        }

        public HotReloadSystems(Systems systems)
        {
            _systems = systems;
#if UNITY_EDITOR
            HotReloadCompiler.PrewarmCache();
            HotReloadCompiler.OnSystemsCompiled += OnSystemsCompiled;
#endif
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
#if UNITY_EDITOR
            HotReloadCompiler.OnSystemsCompiled -= OnSystemsCompiled;
            foreach (var entry in _entries)
                HotReloadWatcher.StopWatching(entry.FilePath);
#endif
            dbug.log("[HotReload] Disposed");
        }

        public void StartTracking()
        {
#if UNITY_EDITOR
            TrackRunnerList(_systems.runners);
#endif
        }

        public void SetSystems(Systems systems)
        {
            _systems = systems;
        }

        public void OnUpdate(float dt, float time)
        {
            if (_systems != null)
                _systems.OnUpdate(dt, time);
        }

#if UNITY_EDITOR
        private void TrackRunnerList(List<ISystemRunner> runners)
        {
            for (int i = 0; i < runners.Count; i++)
            {
                var runner = runners[i];
                var runnerName = runner.Name;

                var systemMethod = FindSystemMethodByRunnerName(runnerName);
                if (systemMethod == null) continue;

                var declaringType = systemMethod.DeclaringType;
                var methodName = systemMethod.Name;
                var sourcePath = TryFindSourceFile(declaringType);

                var modeField = runner.GetType().GetField("Mode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var threadMode = modeField != null ? (Threads)modeField.GetValue(runner) : Threads.Main;

                if (!string.IsNullOrEmpty(sourcePath))
                {
                    var fullPath = sourcePath;
                    if (!Path.IsPathRooted(sourcePath))
                        fullPath = Path.GetFullPath(Path.Combine(
                            UnityEngine.Application.dataPath, "..", sourcePath));
                    HotReloadWatcher.Watch(fullPath);
                    sourcePath = fullPath;
                }

                var entry = new SystemEntry
                {
                    FilePath = sourcePath ?? "",
                    MethodName = methodName,
                    DeclaringTypeName = declaringType?.Name ?? "",
                    ThreadMode = threadMode,
                    RunnerIndex = i
                };
                _entries.Add(entry);
            }
        }

        private unsafe void OnSystemsCompiled(string filePath, MethodInfo[] methods, Func<IntPtr, Threads, ISystemRunner>[] factories)
        {
            if (_systems == null) return;

            var matchedMethods = new HashSet<int>();

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.FilePath != filePath) continue;

                for (int j = 0; j < methods.Length; j++)
                {
                    if (methods[j].Name == entry.MethodName &&
                        methods[j].DeclaringType?.Name == entry.DeclaringTypeName)
                    {
                        var worldPtr = _systems.World.UnsafeWorld;
                        ISystemRunner runner;
                        try
                        {
                            runner = factories[j]((IntPtr)worldPtr, entry.ThreadMode);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[HotReload] Factory failed for {entry.MethodName}: {ex}");
                            break;
                        }

                        if (entry.RunnerIndex >= 0 && entry.RunnerIndex < _systems.runners.Count)
                            _systems.runners[entry.RunnerIndex] = runner;
                        else
                            _systems.runners.Add(runner);

                        matchedMethods.Add(j);
                        break;
                    }
                }
            }

            for (int j = 0; j < methods.Length; j++)
            {
                if (matchedMethods.Contains(j)) continue;

                var method = methods[j];
                var worldPtr = _systems.World.UnsafeWorld;
                ISystemRunner runner;
                try
                {
                    runner = factories[j]((IntPtr)worldPtr, Threads.Main);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HotReload] Factory failed for new system {method.Name}: {ex}");
                    continue;
                }

                var runnerIndex = _systems.runners.Count;
                _systems.runners.Add(runner);

                var declaringType = method.DeclaringType;
                _entries.Add(new SystemEntry
                {
                    FilePath = filePath,
                    MethodName = method.Name,
                    DeclaringTypeName = declaringType?.Name ?? "",
                    ThreadMode = Threads.Main,
                    RunnerIndex = runnerIndex
                });

                Debug.Log($"[HotReload] Added new system: {method.Name}");
            }
        }

        private static readonly Dictionary<Type, string> SourceFileCache = new Dictionary<Type, string>();
        private static Dictionary<string, MethodInfo> _runnerNameCache;

        private static void EnsureRunnerNameCache()
        {
            if (_runnerNameCache != null) return;
            _runnerNameCache = new Dictionary<string, MethodInfo>();

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
                                var key = $"{type.Name}_{method.Name}Job";
                                if (!_runnerNameCache.ContainsKey(key))
                                    _runnerNameCache[key] = method;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static MethodInfo FindSystemMethodByRunnerName(string runnerName)
        {
            EnsureRunnerNameCache();
            return _runnerNameCache.TryGetValue(runnerName, out var method) ? method : null;
        }

        private static string TryFindSourceFile(Type declaringType)
        {
            if (declaringType == null) return null;

            if (SourceFileCache.TryGetValue(declaringType, out var cached))
                return cached;

            var typeName = declaringType.Name;
            var typeNamespace = declaringType.Namespace;
            var assetsPath = UnityEngine.Application.dataPath;

            var directMatch = TryFindFile(assetsPath, typeName + ".cs");
            if (directMatch != null)
            {
                SourceFileCache[declaringType] = directMatch;
                return directMatch;
            }

            foreach (var file in Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories))
            {
                try
                {
                    var content = File.ReadAllText(file);
                    if ((content.Contains($"class {typeName}") || content.Contains($"struct {typeName}") ||
                         content.Contains($"static class {typeName}")) &&
                        (string.IsNullOrEmpty(typeNamespace) || content.Contains($"namespace {typeNamespace}")))
                    {
                        var fullPath = Path.GetFullPath(file);
                        SourceFileCache[declaringType] = fullPath;
                        return fullPath;
                    }
                }
                catch { }
            }

            SourceFileCache[declaringType] = null;
            return null;
        }

        private static string TryFindFile(string directory, string fileName)
        {
            foreach (var file in Directory.GetFiles(directory, fileName, SearchOption.AllDirectories))
            {
                return Path.GetFullPath(file);
            }
            return null;
        }
#endif
    }

    public static class SystemsHotReloadExtensions
    {
        public static Systems AddHotReload(this Systems systems)
        {
            var hts = new HotReloadSystems(systems);
            hts.StartTracking();
            systems.onWorldDispose +=  (ref World _) =>
            {
                hts.Dispose();
            };
            return systems;
        }
    }
}
