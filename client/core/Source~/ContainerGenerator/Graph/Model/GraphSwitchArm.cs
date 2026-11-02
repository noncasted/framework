namespace ContainerGenerator {
    internal sealed class GraphSwitchArm {
        private string _implementationType = "";

        public string Discriminant { get; init; } = "";
        public string ImplementationType { get => _implementationType; init => _implementationType = value; }
        public string ParameterExpression { get; private set; } = "";
        public string ParameterType { get; private set; } = "";

        public void SetParameter(string expression, string type) {
            ParameterExpression = expression;
            ParameterType = type;
        }

        public void Close(string implementationType, string parameterType) {
            _implementationType = implementationType;
            ParameterType = parameterType;
        }

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
