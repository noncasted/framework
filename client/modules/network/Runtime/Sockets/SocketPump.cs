using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public static class SocketPump
    {
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
                    Debug.LogException(exception);
                }
            }
        }
    }
}
