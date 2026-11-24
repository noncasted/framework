using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Internal
{
    // Registrations are a static table of the generated class, shared by all its instances.
    // Children and loaded assets are appended during building (GeneratedScopeCreator, ContainerRegistryDebug).
    public sealed class ContainerDiagnostics : IContainerDiagnostics
    {
        public ContainerDiagnostics(
            string name,
            IContainerDiagnostics parent,
            IReadOnlyList<RegistrationInfo> registrations,
            IReadOnlyList<int> buildOrder)
        {
            Name = name;
            Parent = parent;
            Registrations = registrations ?? Array.Empty<RegistrationInfo>();
            BuildOrder = buildOrder ?? Array.Empty<int>();
        }

        private List<IContainerDiagnostics> _children;

        // Allocations are recorded only in the editor: one record per (phase, listener) pair, repeats are summed.
        private List<AllocationRecord> _allocations;
        private Dictionary<(Type, Type), int> _allocationIndices;

        public string Name { get; }
        public IContainerDiagnostics Parent { get; }

        public IReadOnlyList<IContainerDiagnostics> Children => (IReadOnlyList<IContainerDiagnostics>)_children ??
                                                                Array.Empty<IContainerDiagnostics>();

        public IReadOnlyList<RegistrationInfo> Registrations { get; }
        public IReadOnlyList<int> BuildOrder { get; }
        public IReadOnlyList<LoadedAssetInfo> LoadedAssets { get; private set; } = Array.Empty<LoadedAssetInfo>();

        public IReadOnlyList<AllocationRecord> Allocations => (IReadOnlyList<AllocationRecord>)_allocations ??
                                                              Array.Empty<AllocationRecord>();

        // Thread counter: building and phases run on the main thread, other editor threads are not counted.
        // Editor only: the icall is not implemented in WebGL and logs an error on every call.
        internal static long AllocatedBytes()
        {
#if UNITY_EDITOR
            return GC.GetAllocatedBytesForCurrentThread();
#else
            return 0;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        internal void RecordAllocation(Type phase, Type listener, long bytes)
        {
            var isBuild = phase == null;

            if (bytes == 0 && isBuild == false)
                return;

            _allocations ??= new List<AllocationRecord>();
            _allocationIndices ??= new Dictionary<(Type, Type), int>();

            if (_allocationIndices.TryGetValue((phase, listener), out var index) == true)
            {
                var previous = _allocations[index];

                _allocations[index] = new AllocationRecord(
                    phase,
                    listener,
                    previous.Bytes + bytes,
                    previous.Calls + 1);
            }
            else
            {
                _allocationIndices.Add((phase, listener), _allocations.Count);
                _allocations.Add(new AllocationRecord(phase, listener, bytes, 1));
            }

            ContainerRegistryDebug.RecordClassAllocation(Name, bytes, isBuild);
        }

        internal void SetLoadedAssets(IReadOnlyList<LoadedAssetInfo> loadedAssets)
        {
            LoadedAssets = loadedAssets ?? Array.Empty<LoadedAssetInfo>();
        }

        internal void AddChild(IContainerDiagnostics child)
        {
            _children ??= new List<IContainerDiagnostics>();
            _children.Add(child);
        }

        internal bool RemoveChild(IContainerDiagnostics child)
        {
            return _children != null && _children.Remove(child);
        }
    }
}