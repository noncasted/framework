using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface INetworkSessionCallbackEntry
    {
    }

    public interface INetworkSessionCallbacks
    {
        void Register(INetworkSessionCallbackEntry callback);

        UniTask InvokeSessionSetupCompleted(IReadOnlyLifetime lifetime);
    }

    public interface INetworkSessionSetupCompleted : INetworkSessionCallbackEntry
    {
        UniTask OnSessionSetupCompleted(IReadOnlyLifetime lifetime);
    }

    public class NetworkSessionCallbacks : INetworkSessionCallbacks
    {
        private readonly List<INetworkSessionSetupCompleted> _setupCompleted = new();

        public void Register(INetworkSessionCallbackEntry callback)
        {
            switch (callback)
            {
                case INetworkSessionSetupCompleted completed:
                {
                    _setupCompleted.Add(completed);
                    break;
                }
            }
        }

        public async UniTask InvokeSessionSetupCompleted(IReadOnlyLifetime lifetime)
        {
            foreach (var callback in _setupCompleted)
                await callback.OnSessionSetupCompleted(lifetime);
        }
    }
}