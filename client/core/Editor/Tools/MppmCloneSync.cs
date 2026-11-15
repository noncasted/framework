using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Internal
{
    /// <summary>
    /// Workaround for two Multiplayer Play Mode (6000.7) bugs for clones with Keep Active.
    ///
    /// Main editor: the "clone connected" flag lives in SessionState, but System.Text.Json does not write
    /// fields, so it is reset after every domain reload. A clone reports itself once per
    /// process, so the scenario deploy waits for it forever. Before the reload we remember which clones were
    /// connected, and after the reload restore the flag for those whose process stayed the same.
    ///
    /// Clone: the assemblies are shared with the main editor, but the clone does not reload them and keeps
    /// running old code. We watch ScriptAssemblies and reload the domain outside Play.
    ///
    /// MPPM is internal, so everything goes through reflection: if the API changes, the script silently turns off.
    /// </summary>
    [InitializeOnLoad]
    [NoAutoStaticsCleanup]
    public static class MppmCloneSync
    {
        private const string SnapshotKey = "Internal.MppmCloneSync.Communicative";
        private const double MainRestoreTimeout = 30d;
        private const double ClonePollInterval = 1d;

        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private static double _restoreDeadline;

        private static string _assembliesDirectory;
        private static DateTime _loadedAssembliesTime;
        private static DateTime _pendingAssembliesTime;
        private static double _nextPoll;

        static MppmCloneSync()
        {
            if (IsClone() == true)
            {
                StartCloneWatch();
                return;
            }

            AssemblyReloadEvents.beforeAssemblyReload += SaveCommunicative;

            if (string.IsNullOrEmpty(SessionState.GetString(SnapshotKey, string.Empty)) == true)
                return;

            _restoreDeadline = EditorApplication.timeSinceStartup + MainRestoreTimeout;
            EditorApplication.update += RestoreCommunicative;
        }

        private static bool IsClone()
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), "--virtual-project-clone") >= 0;
        }

        private static void SaveCommunicative()
        {
            var snapshot = new List<string>();

            var clones = ReadClones();

            if (clones == null)
                return;

            foreach (var (key, state, processId) in clones)
            {
                if (GetCommunicative(state) == true && processId > 0)
                    snapshot.Add($"{key}:{processId}");
            }

            SessionState.SetString(SnapshotKey, string.Join(";", snapshot));
        }

        private static void RestoreCommunicative()
        {
            if (EditorApplication.timeSinceStartup > _restoreDeadline)
            {
                Finish();
                return;
            }

            // The MPPM context is created later than our static constructor.
            var clones = ReadClones();

            if (clones == null)
                return;

            var snapshot = SessionState.GetString(SnapshotKey, string.Empty).Split(';');

            foreach (var (key, state, processId) in clones)
            {
                if (GetCommunicative(state) == true || IsAlive(processId) == false)
                    continue;

                if (Array.IndexOf(snapshot, $"{key}:{processId}") < 0)
                    continue;

                state.GetType().GetField("IsCommunicative", Members)?.SetValue(state, true);
                Debug.Log($"[MPPM] Restored connection state of clone {key} after domain reload");
            }

            Finish();

            static void Finish()
            {
                SessionState.EraseString(SnapshotKey);
                EditorApplication.update -= RestoreCommunicative;
            }
        }

        // null while the main editor context is not ready, the MPPM API was not found or its read failed.
        private static List<(object Key, object State, int ProcessId)> ReadClones()
        {
            try
            {
                var contexts = Type.GetType("Unity.Multiplayer.PlayMode.Editor.EditorContexts, UnityEditor.MultiplayerModule");
                var context = contexts?.GetProperty("MainEditorContext", Statics)?.GetValue(null);

                if (context == null)
                    return null;

                var states = context.GetType().GetProperty("StateRepository", Members)?.GetValue(context);
                var processes = context.GetType().GetProperty("ProcessRepository", Members)?.GetValue(context);
                var getAll = states?.GetType().GetMethod("GetAll", Members);
                var tryGetProcess = processes?.GetType().GetMethod("TryGetValue", Members);

                if (getAll == null || tryGetProcess == null)
                    return null;

                var result = new List<(object, object, int)>();

                foreach (var entry in (IEnumerable)getAll.Invoke(states, null))
                {
                    var key = entry.GetType().GetProperty("Key")?.GetValue(entry);
                    var state = entry.GetType().GetProperty("Value")?.GetValue(entry);

                    if (key == null || state == null)
                        continue;

                    var args = new[] { key, null };
                    var processId = -1;

                    if ((bool)tryGetProcess.Invoke(processes, args) == true && args[1] != null)
                        processId = (int)args[1].GetType().GetProperty("Value", Members).GetValue(args[1]);

                    result.Add((key, state, processId));
                }

                return result;
            }
            catch (Exception exception)
            {
                // Not an empty list: taken for "no clones", the restore would erase the snapshot and the save write an
                // empty one. The restore polls again until its deadline.
                Debug.LogWarning($"[MPPM] Clone sync is unavailable: {exception.Message}");
                return null;
            }
        }

        private static bool GetCommunicative(object state)
        {
            return state.GetType().GetField("IsCommunicative", Members)?.GetValue(state) is true;
        }

        private static bool IsAlive(int processId)
        {
            if (processId <= 0)
                return false;

            try
            {
                return Process.GetProcessById(processId).HasExited == false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void StartCloneWatch()
        {
            _assembliesDirectory = Path.GetDirectoryName(typeof(MppmCloneSync).Assembly.GetLoadedAssemblyPath());
            _loadedAssembliesTime = GetAssembliesTime();
            _pendingAssembliesTime = _loadedAssembliesTime;

            EditorApplication.update += PollAssemblies;
        }

        private static void PollAssemblies()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll)
                return;

            _nextPoll = EditorApplication.timeSinceStartup + ClonePollInterval;

            if (EditorApplication.isPlayingOrWillChangePlaymode == true
                || EditorApplication.isCompiling == true
                || EditorApplication.isUpdating == true)
            {
                return;
            }

            var time = GetAssembliesTime();

            if (time <= _loadedAssembliesTime)
                return;

            // The main editor copies assemblies one by one: wait until the timestamp stops changing.
            if (time != _pendingAssembliesTime)
            {
                _pendingAssembliesTime = time;
                return;
            }

            _loadedAssembliesTime = time;
            Debug.Log("[MPPM] Script assemblies changed in the main editor, reloading clone domain");
            EditorUtility.RequestScriptReload();
        }

        private static DateTime GetAssembliesTime()
        {
            var latest = DateTime.MinValue;

            foreach (var file in Directory.GetFiles(_assembliesDirectory, "*.dll"))
            {
                var time = File.GetLastWriteTimeUtc(file);

                if (time > latest)
                    latest = time;
            }

            return latest;
        }
    }
}
