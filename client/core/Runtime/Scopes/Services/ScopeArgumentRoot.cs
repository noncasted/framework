using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // A root with an argument as the callback of the scope (ScopeLoadOptions.Create): a method of an object instead of a
    // lambda over the two, which the generator reports as a root without a name (CINGR017). Not named Construct: the
    // generator takes a Construct with a builder for a root, and a root of a generic type is CINGR016; the root of the
    // scope is the method it wraps (ScopeLoadOptions.Create passes its MethodInfo).
    internal sealed class ScopeArgumentRoot<TArg>
    {
        public ScopeArgumentRoot(Func<IScopeBuilder, TArg, UniTask> construct, TArg arg)
        {
            _construct = construct;
            _arg = arg;
        }

        private readonly Func<IScopeBuilder, TArg, UniTask> _construct;
        private readonly TArg _arg;

        public UniTask Invoke(IScopeBuilder builder)
        {
            return _construct(builder, _arg);
        }
    }
}
