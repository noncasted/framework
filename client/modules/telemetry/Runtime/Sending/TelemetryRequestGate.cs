using System;
using Cysharp.Threading.Tasks;

namespace Global.Telemetry
{
    // One request of the module in flight: the sessions and the startup trace take their turns here, in the order they came. A
    // free gate runs the request at once. Every call of a transport ends, so the wait is bounded by the time of the requests ahead.
    public class TelemetryRequestGate
    {
        private UniTask _last = UniTask.CompletedTask;

        public async UniTask<T> Run<T>(Func<UniTask<T>> request)
        {
            var done = TakeTurn(out var turn);

            try
            {
                await turn;
                return await request();
            }
            finally
            {
                done.TrySetResult();
            }
        }

        public async UniTask Run(Func<UniTask> request)
        {
            var done = TakeTurn(out var turn);

            try
            {
                await turn;
                await request();
            }
            finally
            {
                done.TrySetResult();
            }
        }

        private UniTaskCompletionSource TakeTurn(out UniTask turn)
        {
            var done = new UniTaskCompletionSource();

            turn = _last;
            _last = done.Task;

            return done;
        }
    }
}
