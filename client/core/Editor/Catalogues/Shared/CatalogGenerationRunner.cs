using System;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Catalog generators are triggered by editor events in bursts, so a run is
    // guarded against reentrancy, postponed during import/compilation and debounced.
    public sealed class CatalogGenerationRunner
    {
        private const float DebounceSeconds = 0.5f;

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
                Debug.LogError($"[{_logTag}] Generate failed: {exception}");
            }
            finally
            {
                _isGenerating = false;
            }
        }

        // Synchronous run for Tools/Foundation/Regenerate All: no debounce and no postponing, a failure reaches the
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

        public void RunDelayed()
        {
            EditorApplication.delayCall += Run;
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
            Run();
        }
    }
}