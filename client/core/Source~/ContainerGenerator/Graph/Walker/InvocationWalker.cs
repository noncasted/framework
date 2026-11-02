using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class InvocationWalker {
        private readonly GraphRelevance _graphRelevance;
        private readonly InstallerClassifier _installerClassifier;
        private readonly RegistrationHandlers _registrationHandlers;
        private readonly SourceMethods _sourceMethods;
        private readonly IGraphWalk _walk;
        private readonly WalkDiagnostics _walkDiagnostics;

        public InvocationWalker(GraphRelevance graphRelevance, InstallerClassifier installerClassifier, RegistrationHandlers registrationHandlers, SourceMethods sourceMethods, IGraphWalk walk, WalkDiagnostics walkDiagnostics) {
            _graphRelevance = graphRelevance;
            _installerClassifier = installerClassifier;
            _registrationHandlers = registrationHandlers;
            _sourceMethods = sourceMethods;
            _walk = walk;
            _walkDiagnostics = walkDiagnostics;
        }

        public void WalkInvocation(WalkState state, InvocationExpressionSyntax invocation) {
            var symbol = state.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            var name = symbol != null ? symbol.OriginalDefinition.Name : WalkSyntax.MethodName(invocation);

            foreach (var argument in invocation.ArgumentList.Arguments) {
                var value = WalkSyntax.Unwrap(argument.Expression);
                if (value is SimpleLambdaExpressionSyntax || value is ParenthesizedLambdaExpressionSyntax || value is AnonymousMethodExpressionSyntax)
                    _walk.WalkLambda(state, value);
            }

            var primitive = Primitives.Of(symbol, name);

            if (primitive == PrimitiveKind.Register) {
                _registrationHandlers.HandleRegister(state, invocation, symbol);
                return;
            }

            if (primitive == PrimitiveKind.RegisterInstance) {
                _registrationHandlers.HandleRegisterInstance(state, invocation, symbol);
                return;
            }

            if (primitive == PrimitiveKind.RegisterComponent) {
                _registrationHandlers.HandleRegisterComponent(state, invocation, symbol);
                return;
            }

            if (primitive == PrimitiveKind.As) {
                _registrationHandlers.HandleAs(state, invocation, symbol);
                return;
            }

            if (primitive == PrimitiveKind.AsSelf) {
                _registrationHandlers.HandleAsSelf(state, invocation);
                return;
            }

            if (primitive == PrimitiveKind.AsSelfResolvable) {
                _walk.WalkReceiver(state, invocation);
                return;
            }

            if (primitive == PrimitiveKind.WithParameter) {
                _registrationHandlers.HandleWithParameter(state, invocation, symbol);
                return;
            }

            if (primitive == PrimitiveKind.WithScopeLifetime) {
                _registrationHandlers.HandleWithScopeLifetime(state, invocation);
                return;
            }

            if (primitive == PrimitiveKind.Inject) {
                _registrationHandlers.HandleInject(state, invocation);
                return;
            }

            if (primitive == PrimitiveKind.Injectable) {
                _registrationHandlers.HandleInjectable(state, invocation, symbol);
                return;
            }

            if (primitive == PrimitiveKind.Instantiate) {
                _registrationHandlers.HandleInstantiate(state, invocation);
                return;
            }

            if (primitive == PrimitiveKind.SceneServices) {
                _registrationHandlers.HandleSceneServices(state, invocation);
                return;
            }

            if (IsBenign(name)) {
                _walk.WalkReceiver(state, invocation);
                foreach (var argument in invocation.ArgumentList.Arguments)
                    _walk.WalkExpression(state, argument.Expression, allowUnknown: true);
                return;
            }

            if (symbol == null && IsAddInstaller(name, state, invocation)) {
                _walk.WalkReceiver(state, invocation);
                AddCall(state, name, null);
                return;
            }

            if (symbol != null && InstallerClassifier.IsPrimitiveBuilder(symbol) == false &&
                (_installerClassifier.IsInstaller(symbol) || IsAddInstaller(name, state, invocation))) {
                _walk.WalkReceiver(state, invocation);
                var callee = _walk.WalkMethod(symbol, false, linkToContainer: false);
                AddCall(state, callee.Id, symbol);
                state.SetLastRegistration(null);
                return;
            }

            if (symbol != null && HasSyntax(symbol) == false && _graphRelevance.ReceiverIsGraphRelevant(state, invocation)) {
                AddCall(state, MethodIds.Of(InstallerClassifier.Normalize(symbol)), symbol);
                return;
            }

            if (_graphRelevance.ReceiverIsGraphRelevant(state, invocation) == false) {
                _walk.WalkReceiver(state, invocation);
                return;
            }

            if (symbol == null) {
                _walkDiagnostics.Error(state, invocation, "unresolved invocation '" + invocation.ToString() + "'", GraphDescriptors.UnresolvedInstaller);
                return;
            }

            _walkDiagnostics.Error(state, invocation, "invocation '" + name + "'");
        }

        private bool IsAddInstaller(string name, WalkState state, InvocationExpressionSyntax invocation) {
            if (name.StartsWith("Add", StringComparison.Ordinal) == false)
                return false;
            return _graphRelevance.ReceiverIsGraphRelevant(state, invocation);
        }

        private static void AddCall(WalkState state, string id, IMethodSymbol? constructed) {
            if (string.IsNullOrEmpty(id))
                return;

            var typeArgs = TypeSubstitution.EncodeTypeArgs(constructed);
            if (string.IsNullOrEmpty(typeArgs) && state.Result.Calls.Contains(id)) {
                state.SetLastCall(state.Result.Calls.IndexOf(id));
                return;
            }

            state.Result.AddCall(id, state.TakeOrdinal(), typeArgs);
            state.SetLastCall(state.Result.Calls.Count - 1);
        }

        private bool HasSyntax(IMethodSymbol method) {
            return _sourceMethods.LocalSyntax(method.OriginalDefinition) != null;
        }

        private static bool IsBenign(string name) {
            switch (name) {
                case "Measure":
                case "Scope":
                case "Start":
                case "Stop":
                case "Dispose":
                case "LoadPrefabGroup":
                case "RequestPrefabGroup":
                case "RequestSpriteGroup":
                case "LoadSpriteGroup":
                case "LoadAssetGroup":
                case "RequestAssetGroup":
                case "FindOrLoadScene":
                case "FindOrLoadSceneWithServices":
                case "WhenAll":
                case "GetComponent":
                case "SetActive":
                case "EnsureLoaded":
                case "NoAwait":
                case "Advise":
                case "KeepAlive":
                case "MoveToModules":
                case "GetType":
                case "ToString":
                case "Equals":
                case "GetHashCode":
                    return true;
                default:
                    return false;
            }
        }
    }
}
