using System;

namespace Internal
{
    // Phase == null: container build (generated constructor). Listener == null: ResolveAll of the phase list.
    public readonly struct AllocationRecord
    {
        public AllocationRecord(Type phase, Type listener, long bytes, int calls)
        {
            Phase = phase;
            Listener = listener;
            Bytes = bytes;
            Calls = calls;
        }

        public readonly Type Phase;
        public readonly Type Listener;
        public readonly long Bytes;
        public readonly int Calls;
    }

    // Totals of one generated container class since Play started: disposed containers stay counted.
    public readonly struct ContainerClassAllocations
    {
        public ContainerClassAllocations(string name, int built, long buildBytes, long phaseBytes)
        {
            Name = name;
            Built = built;
            BuildBytes = buildBytes;
            PhaseBytes = phaseBytes;
        }

        public readonly string Name;
        public readonly int Built;
        public readonly long BuildBytes;
        public readonly long PhaseBytes;
    }
}
