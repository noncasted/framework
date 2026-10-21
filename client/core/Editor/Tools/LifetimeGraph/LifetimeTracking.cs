using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    public sealed class LifetimeRecord
    {
        public LifetimeRecord(int id, int parentId, int secondParentId, string owner, string stack, float createdAt)
        {
            Id = id;
            ParentId = parentId;
            SecondParentId = secondParentId;
            Owner = owner;
            Stack = stack;
            CreatedAt = createdAt;
        }

        public int Id { get; }
        public int ParentId { get; }

        // Second Intersect source; 0 for regular lifetimes.
        public int SecondParentId { get; }
        public string Owner { get; }
        public string Stack { get; }

        // Time since Play Mode start (editor clock when the queue is drained: realtimeSinceStartup resets in Play).
        public float CreatedAt { get; }
    }

    /// <summary>
    /// Main-thread model of alive lifetimes, fed by <see cref="LifetimeAnalytics"/>.
    /// Drains every editor update even when the graph window is closed, so the queue never grows.
    /// </summary>
    [InitializeOnLoad]
    [NoAutoStaticsCleanup]
    public static class LifetimeTracking
    {
        private const string EnabledPrefsKey = "Internal.LifetimeGraph.TrackingEnabled";
        private const string ToggleMenuPath = "Tools/Lifetime Graph/Track Lifetimes";

        private static readonly Dictionary<int, LifetimeRecord> _alive = new();
        private static float _playStartedAt;

        static LifetimeTracking()
        {
            EditorApplication.update += Drain;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            // After a domain reload on entering Play, ExitingEditMode has already passed: enable here.
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
            {
                _playStartedAt = (float)EditorApplication.timeSinceStartup;
                LifetimeAnalytics.IsEnabled = IsTrackingEnabled;
            }
        }

        public static IReadOnlyDictionary<int, LifetimeRecord> Alive => _alive;

        // Grows on every change of the set of live lifetimes: the graph compares it with its own version.
        public static int Version { get; private set; }

        public static bool IsTrackingEnabled
        {
            get => EditorPrefs.GetBool(EnabledPrefsKey, true);
            set
            {
                EditorPrefs.SetBool(EnabledPrefsKey, value);
                LifetimeAnalytics.IsEnabled = value && EditorApplication.isPlaying;
            }
        }

        [MenuItem(ToggleMenuPath, priority = 1)]
        private static void ToggleTracking()
        {
            IsTrackingEnabled = IsTrackingEnabled == false;
        }

        [MenuItem(ToggleMenuPath, true)]
        private static bool ValidateToggleTracking()
        {
            Menu.SetChecked(ToggleMenuPath, IsTrackingEnabled);
            return true;
        }

        public static void Clear()
        {
            LifetimeAnalytics.ClearQueue();
            _alive.Clear();
            Version++;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Clear();
                    _playStartedAt = (float)EditorApplication.timeSinceStartup;
                    LifetimeAnalytics.IsEnabled = IsTrackingEnabled;
                    break;

                // Those that survived exiting Play stay in the model: these are the leak candidates.
                case PlayModeStateChange.EnteredEditMode:
                    Drain();
                    LifetimeAnalytics.IsEnabled = false;
                    break;
            }
        }

        private static void Drain()
        {
            var changed = false;
            var now = (float)EditorApplication.timeSinceStartup - _playStartedAt;

            while (LifetimeAnalytics.TryDequeue(out var lifetimeEvent))
            {
                switch (lifetimeEvent.Kind)
                {
                    case LifetimeEventKind.Created:
                        _alive[lifetimeEvent.Id] = new LifetimeRecord(
                            lifetimeEvent.Id,
                            lifetimeEvent.ParentId,
                            lifetimeEvent.SecondParentId,
                            lifetimeEvent.Owner,
                            lifetimeEvent.Stack,
                            now);
                        break;

                    case LifetimeEventKind.Terminated:
                        _alive.Remove(lifetimeEvent.Id);
                        break;
                }

                changed = true;
            }

            if (changed == true)
                Version++;
        }
    }
}
