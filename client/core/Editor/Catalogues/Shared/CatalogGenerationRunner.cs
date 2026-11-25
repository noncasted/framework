using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;

namespace Internal
{
    // Catalog generators are triggered by editor events in bursts, so a run is
    // guarded against reentrancy, postponed during import/compilation and debounced.
    [AutoStaticsCleanup]
    public sealed partial class CatalogGenerationRunner
    {
        private const float DebounceSeconds = 0.5f;

        // Pauses of the scheduled and delayed runs, counted: the temporary assets of the editor tests schedule the
        // generators, and a generation of the catalogues of the project between two tests costs seconds. A run due
        // while paused is dropped; Run, RunNow and the menu items still generate.
        private static int _pauses;

        [NoAutoStaticsCleanup] private static readonly ILog _log = Logs.Of<CatalogGenerationRunner>();

        private readonly string _logTag;
        private readonly Action _generate;

        private bool _isGenerating;
        private bool _isScheduled;
        private bool _isWaitingPlayMode;
        private double _scheduledTime;

        public CatalogGenerationRunner(string logTag, Action generate)
        {
            _logTag = logTag;
            _generate = generate;
        }

        public void Run()
        {
            // Parallel import workers also load the editor domain; writing assets from there
            // is forbidden and breaks the main process's SourceAssetDB.
            if (_isGenerating || AssetDatabase.IsAssetImportWorkerProcess())
                return;

            // Assets are not regenerated in Play mode: the run is postponed until Play mode exit,
            // otherwise markup edits made during play are lost.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                WaitPlayModeExit();
                return;
            }

            if (EditorApplication.isCompiling)
            {
                Schedule();
                return;
            }

            if (EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            _isGenerating = true;

            try
            {
                _generate();
            }
            catch (Exception exception)
            {
                _log.Error(exception, "{Generator}: generate failed", _logTag);
            }
            finally
            {
                _isGenerating = false;
            }
        }

        // Synchronous run for Tools/Framework/Codegen/Regenerate All: no debounce and no postponing, a failure reaches the
        // caller. The caller checks Play mode and compilation before the first step.
        public void RunNow()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                throw new InvalidOperationException($"[{_logTag}] Generation is not allowed in an import worker.");

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException($"[{_logTag}] Generation is not allowed in Play mode.");

            if (_isGenerating)
                throw new InvalidOperationException($"[{_logTag}] Generation is already running.");

            _isGenerating = true;

            try
            {
                _generate();
            }
            finally
            {
                _isGenerating = false;
            }
        }

        private void WaitPlayModeExit()
        {
            if (_isWaitingPlayMode)
                return;

            _isWaitingPlayMode = true;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _isWaitingPlayMode = false;
            Run();
        }

        public static void PauseScheduled()
        {
            _pauses++;
        }

        public static void ResumeScheduled()
        {
            if (_pauses == 0)
                throw new InvalidOperationException("Scheduled catalogue generation is not paused.");

            _pauses--;
        }

        public void RunDelayed()
        {
            EditorApplication.delayCall += RunUnlessPaused;
        }

        private void RunUnlessPaused()
        {
            if (_pauses == 0)
                Run();
        }

        public void Schedule()
        {
            _scheduledTime = EditorApplication.timeSinceStartup + DebounceSeconds;

            if (_isScheduled)
                return;

            _isScheduled = true;
            EditorApplication.update += TickSchedule;
        }

        private void TickSchedule()
        {
            if (EditorApplication.timeSinceStartup < _scheduledTime)
                return;

            EditorApplication.update -= TickSchedule;
            _isScheduled = false;
            RunUnlessPaused();
        }
    }
}