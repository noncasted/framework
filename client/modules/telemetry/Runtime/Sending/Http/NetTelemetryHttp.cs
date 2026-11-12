#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Net.Http;
using System.Text;
using Cysharp.Threading.Tasks;

namespace Global.Telemetry
{
    // System.Net.Http outside the page: the editor and the native players. BaseUrl must be absolute here.
    public class NetTelemetryHttp : ITelemetryHttp
    {
        public NetTelemetryHttp(TelemetryHttpOptions options)
        {
            _options = options;
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds) };
        }

        private readonly TelemetryHttpOptions _options;
        private readonly HttpClient _client;

        public async UniTask<string> Post(string path, string json)
        {
            if (!Uri.TryCreate(_options.BaseUrl + path, UriKind.Absolute, out var url))
                throw new TelemetryHttpException(0, $"no absolute url outside the web page: {_options.BaseUrl}");

            HttpResponseMessage response;

            try
            {
                response = await _client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch (HttpRequestException exception)
            {
                throw new TelemetryHttpException(0, $"network: {exception.Message}");
            }
            catch (OperationCanceledException)
            {
                throw new TelemetryHttpException(0, "timeout");
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    throw new TelemetryHttpException((int)response.StatusCode, $"status {(int)response.StatusCode}");

                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}
#endif
