namespace Internal
{
    public readonly struct ContainerGraphSwitchArm
    {
        public readonly string Discriminant;
        public readonly string ImplementationType;
        public readonly string ParameterExpression;

        public ContainerGraphSwitchArm(string discriminant, string implementationType, string parameterExpression)
        {
            Discriminant = discriminant ?? "";
            ImplementationType = implementationType ?? "";
            ParameterExpression = parameterExpression ?? "";
        }
    }
}
