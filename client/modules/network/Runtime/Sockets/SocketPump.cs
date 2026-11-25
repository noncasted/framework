using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class SocketPump
    {
        private static readonly ILog _log = Logs.Of(typeof(SocketPump));

        // Calls the tick once a frame while the lifetime lives.
        public static async UniTaskVoid Run(IReadOnlyLifetime lifetime, Action tick)
        {
            while (true)
            {
                var isCanceled = await UniTask
                                       .Yield(PlayerLoopTiming.Update, lifetime.Token)
                                       .SuppressCancellationThrow();

                if (isCanceled == true)
                    return;

                try
                {
                    tick();
                }
                catch (Exception exception)
                {
                    _log.Exception(exception);
                }
            }
        }
    }
}
