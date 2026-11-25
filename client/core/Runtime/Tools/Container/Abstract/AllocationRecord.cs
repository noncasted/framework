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
}
