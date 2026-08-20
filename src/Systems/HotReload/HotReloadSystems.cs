using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Wargon.Nukecs.HotReload
{
    public class HotReloadSystems : IDisposable
    {
        private readonly List<SystemEntry> _entries = new();
        private bool _disposed;

        public HotReloadSystems(ref World world)
        {
            Systems = new Systems(ref world);
#if UNITY_EDITOR
            HotReloadCompiler.PrewarmCache();
            HotReloadCompiler.OnSystemsCompiled += OnSystemsCompiled;
#endif
        }

        public HotReloadSystems(Systems systems)
        {
            Systems = systems;
#if UNITY_EDITOR
            HotReloadCompiler.PrewarmCache();
            HotReloadCompiler.OnSystemsCompiled += OnSystemsCompiled;
#endif
        }

        public Systems Systems { get; private set; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
#if UNITY_EDITOR
            HotReloadCompiler.OnSystemsCompiled -= OnSystemsCompiled;
            foreach (var entry in _entries)
                HotReloadWatcher.StopWatching(entry.filePath);
#endif
            dbug.log("[HotReload] Disposed");
        }

        public void StartTracking()
        {
#if UNITY_EDITOR
            Task.Run(() =>
            {
                TrackRunnerList(Systems.onUpdate);
                TrackRunnerList(Systems.onFixedUpdate);
                dbug.log("[HotReload] Start Tracking");
            });
#endif
        }

        public void SetSystems(Systems systems)
        {
            Systems = systems;
        }

        private struct SystemEntry
        {
            public string filePath;
            public string methodName;
            public string declaringTypeName;
            public Threads threadMode;
            public int runnerIndex;
        }

#if UNITY_EDITOR
        private void TrackRunnerList(List<ISystemRunner> runners)
        {
            for (var i = 0; i < runners.Count; i++)
            {
                var runner = runners[i];
                var runnerName = runner.Name;

                var systemMethod = FindSystemMethodByRunnerName(runnerName);
                if (systemMethod == null) continue;

                var declaringType = systemMethod.DeclaringType;
                var methodName = systemMethod.Name;
                var sourcePath = TryFindSourceFile(declaringType);

                var modeField = runner.GetType().GetField("Mode",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var threadMode = modeField != null ? (Threads)modeField.GetValue(runner) : Threads.Main;

                if (!string.IsNullOrEmpty(sourcePath))
                {
                    var fullPath = sourcePath;
                    if (!Path.IsPathRooted(sourcePath))
                        fullPath = Path.GetFullPath(Path.Combine(
                            Application.dataPath, "..", sourcePath));
                    HotReloadWatcher.Watch(fullPath);
                    sourcePath = fullPath;
                }

                var entry = new SystemEntry
                {
                    filePath = sourcePath ?? "",
                    methodName = methodName,
                    declaringTypeName = declaringType?.Name ?? "",
                    threadMode = threadMode,
                    runnerIndex = i
                };
                _entries.Add(entry);
            }
        }

        private unsafe void OnSystemsCompiled(string filePath, MethodInfo[] methods,
            Func<IntPtr, Threads, ISystemRunner>[] factories)
        {
            if (Systems == null) return;

            var matchedMethods = new HashSet<int>();

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.filePath != filePath) continue;

                for (var j = 0; j < methods.Length; j++)
                    if (methods[j].Name == entry.methodName &&
                        methods[j].DeclaringType?.Name == entry.declaringTypeName)
                    {
                        var worldPtr = Systems.World.UnsafeWorld;
                        ISystemRunner runner;
                        try
                        {
                            runner = factories[j]((IntPtr)worldPtr, entry.threadMode);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[HotReload] Factory failed for {entry.methodName}: {ex}");
                            break;
                        }

                        if (entry.runnerIndex >= 0 && entry.runnerIndex < Systems.onUpdate.Count)
                            Systems.onUpdate[entry.runnerIndex] = runner;
                        else
                            Systems.onUpdate.Add(runner);

                        matchedMethods.Add(j);
                        break;
                    }
            }

            for (var j = 0; j < methods.Length; j++)
            {
                if (matchedMethods.Contains(j)) continue;

                var method = methods[j];
                var worldPtr = Systems.World.UnsafeWorld;
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

                var runnerIndex = Systems.onUpdate.Count;
                Systems.onUpdate.Add(runner);

                var declaringType = method.DeclaringType;
                _entries.Add(new SystemEntry
                {
                    filePath = filePath,
                    methodName = method.Name,
                    declaringTypeName = declaringType?.Name ?? "",
                    threadMode = Threads.Main,
                    runnerIndex = runnerIndex
                });

                Debug.Log($"[HotReload] Added new system: {method.Name}");
            }
        }

        private static readonly Dictionary<Type, string> SourceFileCache = new();
        private static Dictionary<string, MethodInfo> _runnerNameCache;

        private static void EnsureRunnerNameCache()
        {
            if (_runnerNameCache != null) return;
            _runnerNameCache = new Dictionary<string, MethodInfo>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                try
                {
                    foreach (var type in asm.GetTypes())
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                           BindingFlags.Static))
                        if (method.GetCustomAttributes(typeof(SystemAttribute), false).Length > 0)
                        {
                            var key = $"{type.Name}_{method.Name}Job";
                            _runnerNameCache.TryAdd(key, method);
                        }
                }
                catch
                {
                    // ignored
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
            var assetsPath = Application.dataPath;

            var directMatch = TryFindFile(assetsPath, typeName + ".cs");
            if (directMatch != null)
            {
                SourceFileCache[declaringType] = directMatch;
                return directMatch;
            }

            foreach (var file in Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories))
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
                catch
                {
                    // ignored
                }

            SourceFileCache[declaringType] = null;
            return null;
        }

        private static string TryFindFile(string directory, string fileName)
        {
            foreach (var file in Directory.GetFiles(directory, fileName, SearchOption.AllDirectories))
                return Path.GetFullPath(file);
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
            systems.onWorldDispose += (ref World _) => { hts.Dispose(); };
            return systems;
        }
    }
}