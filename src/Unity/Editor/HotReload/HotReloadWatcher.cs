using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wargon.Nukecs.HotReload
{
    [InitializeOnLoad]
    public static class HotReloadWatcher
    {
        private static readonly HashSet<string> watchedFiles = new HashSet<string>();
        private static readonly Dictionary<string, FileSystemWatcher> watchers = new Dictionary<string, FileSystemWatcher>();
        private static readonly Queue<string> changedFiles = new Queue<string>();
        private static double lastProcessTime;
        private const double DEBOUNCE_SECONDS = 0.5;

        static HotReloadWatcher()
        {
            EditorApplication.update += ProcessChanges;
        }

        public static void Watch(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (!Path.IsPathRooted(filePath))
                filePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", filePath));

            if (!File.Exists(filePath)) return;

            var fullPath = Path.GetFullPath(filePath);
            if (watchedFiles.Contains(fullPath)) return;
            watchedFiles.Add(fullPath);

            var dir = Path.GetDirectoryName(fullPath);
            var fileName = Path.GetFileName(fullPath);

            if (!watchers.TryGetValue(dir!, out var watcher))
            {
                watcher = new FileSystemWatcher(dir!)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = false
                };

                watcher.Changed += OnFileChanged;
                watcher.EnableRaisingEvents = true;
                watchers[dir!] = watcher;
            }
        }

        public static void StopWatching(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            var fullPath = Path.GetFullPath(filePath);
            watchedFiles.Remove(fullPath);
        }

        public static void StopAll()
        {
            foreach (var kv in watchers)
                kv.Value.Dispose();
            watchers.Clear();
            watchedFiles.Clear();
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var fullPath = Path.GetFullPath(e.FullPath);
            if (!watchedFiles.Contains(fullPath)) return;
            if (!fullPath.EndsWith(".cs")) return;

            lock (changedFiles)
            {
                if (!changedFiles.Contains(fullPath))
                    changedFiles.Enqueue(fullPath);
            }

            lastProcessTime = EditorApplication.timeSinceStartup;
        }

        private static void ProcessChanges()
        {
            if (changedFiles.Count == 0) return;

            if (EditorApplication.timeSinceStartup - lastProcessTime < DEBOUNCE_SECONDS)
                return;

            string filePath;
            lock (changedFiles)
            {
                if (changedFiles.Count == 0) return;
                filePath = changedFiles.Dequeue();
            }

            if (!EditorApplication.isPlaying)
            {
                lock (changedFiles) { changedFiles.Clear(); }
                return;
            }

            HotReloadCompiler.CompileAndReload(filePath);
        }
    }
}
