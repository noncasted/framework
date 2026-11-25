namespace Internal
{
    public readonly struct ContainerGraphRegistration
    {
        public readonly string Kind;
        public readonly string ImplementationType;
        public readonly string[] ServiceTypes;
        public readonly string Lifetime;
        public readonly ContainerGraphOrigin Origin;
        public readonly string Source;
        public readonly string Hole;
        public readonly string File;
        public readonly int Line;
        public readonly ContainerGraphSwitchArm[] Arms;

        public ContainerGraphRegistration(
            string kind,
            string implementationType,
            string[] serviceTypes,
            string lifetime,
            ContainerGraphOrigin origin,
            string source,
            string hole,
            string file,
            int line,
            ContainerGraphSwitchArm[] arms)
        {
            Kind = kind ?? "";
            ImplementationType = implementationType ?? "";
            ServiceTypes = serviceTypes ?? new string[0];
            Lifetime = lifetime ?? "";
            Origin = origin;
            Source = source ?? "";
            Hole = hole ?? "";
            File = file ?? "";
            Line = line;
            Arms = arms ?? new ContainerGraphSwitchArm[0];
        }
    }
}
