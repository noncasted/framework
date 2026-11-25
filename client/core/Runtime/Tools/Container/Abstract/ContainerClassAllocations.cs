namespace Internal
{
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
