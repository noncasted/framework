using System;
using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    // An asset group of the tests: loaded at once; its unload may fail.
    public sealed class ProbeGroup : AssetGroup
    {
        public ProbeGroup(bool failsToUnload = false)
        {
            _failsToUnload = failsToUnload;
        }

        private readonly bool _failsToUnload;

        protected override UniTask LoadGroup()
        {
            return UniTask.CompletedTask;
        }

        protected override void UnloadGroup()
        {
            if (_failsToUnload == true)
                throw new InvalidOperationException("unload failed");
        }
    }
}
