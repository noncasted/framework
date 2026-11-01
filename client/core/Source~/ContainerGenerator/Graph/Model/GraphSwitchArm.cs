namespace ContainerGenerator {
    internal sealed class GraphSwitchArm {
        public string Discriminant = "";
        public string ImplementationType = "";
        public string ParameterExpression = "";
        public string ParameterType = "";

        public GraphSwitchArm Clone() {
            return new GraphSwitchArm {
                Discriminant = Discriminant,
                ImplementationType = ImplementationType,
                ParameterExpression = ParameterExpression,
                ParameterType = ParameterType,
            };
        }
    }
}
