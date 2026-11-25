namespace Internal
{
    public readonly struct ContainerGraphMethod
    {
        public readonly string Id;
        public readonly bool IsRoot;
        public readonly string File;
        public readonly int Line;
        public readonly ContainerGraphRegistration[] Registrations;
        public readonly string[] Calls;

        public ContainerGraphMethod(
            string id,
            bool isRoot,
            string file,
            int line,
            ContainerGraphRegistration[] registrations,
            string[] calls)
        {
            Id = id ?? "";
            IsRoot = isRoot;
            File = file ?? "";
            Line = line;
            Registrations = registrations ?? new ContainerGraphRegistration[0];
            Calls = calls ?? new string[0];
        }
    }
}
