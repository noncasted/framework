using System;

namespace Internal.Tests
{
    internal sealed class GeneratedBenchmarkSession : IBenchmarkSession
    {
        public GeneratedBenchmarkSession(ILifetime lifetime, IContainer root, IContainer match, IContainer card)
        {
            _lifetime = lifetime;
            _root = root;
            _match = match;
            _card = card;
        }

        private readonly ILifetime _lifetime;
        private readonly IContainer _root;
        private readonly IContainer _match;
        private readonly IContainer _card;

        public IContainer Scope(BenchmarkScopeLevel level)
        {
            switch (level)
            {
                case BenchmarkScopeLevel.Root:
                    return _root;
                case BenchmarkScopeLevel.Match:
                    return _match;
                default:
                    return _card;
            }
        }

        public int ResolveAllPhases()
        {
            // Same consume path as EventLoop.ResolveList<T>.
            var count = 0;
            count += _card.ResolveAll<IBenchBaseSetup>().Count;
            count += _card.ResolveAll<IBenchBaseSetupAsync>().Count;
            count += _card.ResolveAll<IBenchSetup>().Count;
            count += _card.ResolveAll<IBenchSetupAsync>().Count;
            count += _card.ResolveAll<IBenchSetupCompletion>().Count;
            count += _card.ResolveAll<IBenchSetupCompletionAsync>().Count;
            count += _card.ResolveAll<IBenchLoaded>().Count;
            count += _card.ResolveAll<IBenchLoadedAsync>().Count;
            count += _card.ResolveAll<IBenchDispose>().Count;
            count += _card.ResolveAll<IBenchDisposeAsync>().Count;
            count += _card.ResolveAll<IBenchSceneService>().Count;
            count += _card.ResolveAll<IBenchEntityComponent>().Count;
            return count;
        }

        public object ResolveSingleton()
        {
            return _card.Resolve<BenchRootTime>();
        }

        public object ResolveTransient()
        {
            return _card.Resolve<BenchCardAction>();
        }

        public object Resolve(BenchmarkScopeLevel level, Type type)
        {
            return Scope(level).Resolve(type);
        }

        public void Dispose()
        {
            _card.Dispose();
            _match.Dispose();
            _root.Dispose();
            _lifetime.Terminate();
        }
    }
}
