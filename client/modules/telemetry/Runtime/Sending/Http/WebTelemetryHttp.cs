#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Cysharp.Threading.Tasks;

namespace Global.Telemetry
{
    // fetch of the page (TelemetryHttp.jslib): a relative url goes to the server of the page, with its cookies. The answer comes in a
    // promise of the page, outside the player loop, so the caller goes on after the next frame starts.
    public class WebTelemetryHttp : ITelemetryHttp
    {
        public WebTelemetryHttp(TelemetryHttpOptions options)
        {
            _options = options;
        }

        // Generic delegates do not cross into the page.
        private delegate void DoneCallback(int id, int status, string text);

        // The callback must be static for AOT: the requests of every scope of the page wait here by their ids.
        private static readonly Dictionary<int, UniTaskCompletionSource<string>> _pending = new();
        private static int _lastId;

        private readonly TelemetryHttpOptions _options;

        public async UniTask<string> Post(string path, string json)
        {
            var id = ++_lastId;
            var completion = new UniTaskCompletionSource<string>();

            _pending.Add(id, completion);
            TelemetryHttpPost(id, _options.BaseUrl + path, json, (int)(_options.TimeoutSeconds * 1000f), OnDone);

            try
            {
                return await completion.Task;
            }
            finally
            {
                await UniTask.Yield();
            }
        }

        [MonoPInvokeCallback(typeof(DoneCallback))]
        private static void OnDone(int id, int status, string text)
        {
            _pending.Remove(id, out var completion);

            if (status >= 200 && status < 300)
                completion.TrySetResult(text);
            else
                completion.TrySetException(new TelemetryHttpException(status, status == 0 ? text : $"status {status}"));
        }

        [DllImport("__Internal")]
        private static extern void TelemetryHttpPost(int id, string url, string body, int timeoutMs, DoneCallback onDone);
    }
}
#endif
