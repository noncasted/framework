using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class ContainerRegistryDebug
    {
        public static IReadOnlyList<IContainerDiagnostics> Roots => _roots;

        public static IViewableDelegate Changed => _changed;

        // Totals per container class since Play started (editor only): destroyed scopes stay in the count.
        public static IReadOnlyList<ContainerClassAllocations> ClassAllocations => _classAllocations;

        // New containers do not create diagnostics while the flag is cleared (benchmark). In a release build
        // (without UNITY_EDITOR and DEBUG) generated classes do not create it at all.
        public static bool IsEnabled { get; private set; } = true;

        private static readonly List<IContainerDiagnostics> _roots = new();
        private static readonly ViewableDelegate _changed = new();
        private static readonly Dictionary<IReadOnlyLifetime, List<LoadedAssetInfo>> _pendingAssets = new();
        private static readonly List<ContainerClassAllocations> _classAllocations = new();
        private static readonly Dictionary<string, int> _classAllocationIndices = new();

        public static void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        // A container with a parent sits in its Children; only tree roots go into Roots.
        internal static void Add(IContainerDiagnostics diagnostics)
        {
#if UNITY_EDITOR || DEBUG
            if (diagnostics == null)
                return;

            if (diagnostics.Parent is ContainerDiagnostics parent)
                parent.AddChild(diagnostics);
            else if (_roots.Contains(diagnostics) == false)
                _roots.Add(diagnostics);
            else
                return;

            _changed.Invoke();
#endif
        }

        internal static void Remove(IContainerDiagnostics diagnostics)
        {
#if UNITY_EDITOR || DEBUG
            if (diagnostics == null)
                return;

            var removed = diagnostics.Parent is ContainerDiagnostics parent
                ? parent.RemoveChild(diagnostics)
                : _roots.Remove(diagnostics);

            if (removed == false)
                return;

            _changed.Invoke();
#endif
        }

        public static void RecordLoadedAsset(IReadOnlyLifetime lifetime, string label, string groupName)
        {
#if UNITY_EDITOR || DEBUG
            if (lifetime == null)
                return;

            if (_pendingAssets.TryGetValue(lifetime, out var list) == false)
            {
                list = new List<LoadedAssetInfo>();
                _pendingAssets.Add(lifetime, list);
            }

            list.Add(new LoadedAssetInfo(label, groupName));
#endif
        }

        internal static IReadOnlyList<LoadedAssetInfo> TakeLoadedAssets(IReadOnlyLifetime lifetime)
        {
#if UNITY_EDITOR || DEBUG
            if (lifetime == null)
                return Array.Empty<LoadedAssetInfo>();

            if (_pendingAssets.TryGetValue(lifetime, out var list) == false)
                return Array.Empty<LoadedAssetInfo>();

            _pendingAssets.Remove(lifetime);
            return list;
#else
            return Array.Empty<LoadedAssetInfo>();
#endif
        }

        internal static void RecordClassAllocation(string name, long bytes, bool isBuild)
        {
#if UNITY_EDITOR
            if (_classAllocationIndices.TryGetValue(name, out var index) == false)
            {
                index = _classAllocations.Count;
                _classAllocationIndices.Add(name, index);
                _classAllocations.Add(new ContainerClassAllocations(name, 0, 0, 0));
            }

            var previous = _classAllocations[index];

            _classAllocations[index] = isBuild
                ? new ContainerClassAllocations(name, previous.Built + 1, previous.BuildBytes + bytes, previous.PhaseBytes)
                : new ContainerClassAllocations(name, previous.Built, previous.BuildBytes, previous.PhaseBytes + bytes);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _roots.Clear();
            _pendingAssets.Clear();
            _classAllocations.Clear();
            _classAllocationIndices.Clear();
        }
    }
}