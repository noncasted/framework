using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Traces are written to disk: measuring happens in Play mode, but we view it in the editor window
    /// after exiting, when domain objects are already dead.
    /// </summary>
    [NoAutoStaticsCleanup]
    public static class ProfilerTraceStorage
    {
        private const int MaxTraces = 20;

        private static readonly ILog _log = Logs.Of(typeof(ProfilerTraceStorage));

        public static event Action<ProfilerTraceData> Saved;

        public static ProfilerTraceData Last { get; private set; }

        /// <summary>File of the last saved trace. Needed by the editor frame capture: it writes the dump next to it.</summary>
        public static string LastPath { get; private set; }

        /// <summary>
        /// The run happens on freshly recompiled assemblies and a reset asset cache.
        /// Set by an editor hook, cleared after the first saved trace.
        /// </summary>
        public static bool ColdDomain { get; private set; }

        /// <summary>
        /// Traces are stored next to the project, in client/traces: it is more convenient to view them where
        /// the code is, rather than in persistentDataPath. A player has no project folder -
        /// there we fall back to persistentDataPath.
        /// </summary>
        public static string Directory
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "traces"));
#else
                return Path.Combine(Application.persistentDataPath, "ProfilerTraces");
#endif
            }
        }

        /// <summary>
        /// Where the window looks for traces. In the editor it is both the project folder and the player directory:
        /// persistentDataPath in the editor points exactly where a build with the same
        /// company/product writes, so traces from a build show up in the same window.
        /// </summary>
        public static IReadOnlyList<string> SearchDirectories
        {
            get
            {
#if UNITY_EDITOR
                return new[]
                {
                    Directory,
                    Path.Combine(Application.persistentDataPath, "ProfilerTraces")
                };
#else
                return new[] { Directory };
#endif
            }
        }

        public static void SetColdDomain(bool isCold)
        {
            ColdDomain = isCold;
        }

        public static void Save(ProfilerTraceData trace)
        {
            Last = trace;
            LastPath = null;
            ColdDomain = false;

            var directory = Directory;

            if (System.IO.Directory.Exists(directory) == false)
                System.IO.Directory.CreateDirectory(directory);

            trace.Id = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

            var name = $"trace_{trace.Id}.json";
            var path = Path.Combine(directory, name);
            var json = JsonUtility.ToJson(trace);

            File.WriteAllText(path, json);

            LastPath = path;

            Cleanup(directory);

#if UNITY_WEBGL && !UNITY_EDITOR
            // On WebGL the file stays in MEMFS, and a long console line gets truncated -
            // the trace is offered as a download from DevTools.
            ProfilerTraceStore(name, json);
            _log.Info("Trace saved: {Name}. Call downloadProfilerTrace() in DevTools console to get it", name);
#elif !UNITY_EDITOR
            // A desktop build has no editor window - the trace is duplicated into Player.log.
            _log.Info("Trace saved to {Path}", path);
            _log.Info("Trace json: {Json}", json);
#endif

            Saved?.Invoke(trace);
        }

        public static IReadOnlyList<string> List()
        {
            var result = new List<string>();

            foreach (var directory in SearchDirectories)
            {
                if (System.IO.Directory.Exists(directory) == false)
                    continue;

                result.AddRange(System.IO.Directory.GetFiles(directory, "trace_*.json"));
            }

            // The file name is a timestamp, so the newest end up on top regardless
            // of which directory they came from.
            result.Sort((left, right) => string.CompareOrdinal(
                Path.GetFileName(right),
                Path.GetFileName(left)));

            return result;
        }

        public static ProfilerTraceData Load(string path)
        {
            return JsonUtility.FromJson<ProfilerTraceData>(File.ReadAllText(path));
        }

        /// <summary>
        /// The profiler sample dump lies next to the trace and is named by its timestamp:
        /// it is heavy, and there is no reason to carry it inside the trace itself - the window reads it only
        /// when a scope is actually inspected.
        /// </summary>
        public static string FramesPath(string tracePath)
        {
            if (string.IsNullOrEmpty(tracePath))
                return null;

            var directory = Path.GetDirectoryName(tracePath);
            var name = Path.GetFileNameWithoutExtension(tracePath);

            if (string.IsNullOrEmpty(directory) || name.StartsWith("trace_") == false)
                return null;

            return Path.Combine(directory, $"frames_{name.Substring("trace_".Length)}.json");
        }

        /// <summary>Only our own traces are cleaned, there is no reason to touch the player directory.</summary>
        public static void Clear()
        {
            if (System.IO.Directory.Exists(Directory) == false)
                return;

            foreach (var file in System.IO.Directory.GetFiles(Directory, "trace_*.json"))
                File.Delete(file);

            foreach (var file in System.IO.Directory.GetFiles(Directory, "frames_*.json"))
                File.Delete(file);

            Last = null;
            LastPath = null;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ProfilerTraceStore(string name, string json);
#endif

        private static void Cleanup(string directory)
        {
            var files = System.IO.Directory.GetFiles(directory, "trace_*.json");

            if (files.Length <= MaxTraces)
                return;

            Array.Sort(files, StringComparer.Ordinal);

            for (var i = 0; i < files.Length - MaxTraces; i++)
            {
                var frames = FramesPath(files[i]);

                File.Delete(files[i]);

                if (string.IsNullOrEmpty(frames) == false && File.Exists(frames))
                    File.Delete(frames);
            }
        }
    }
}