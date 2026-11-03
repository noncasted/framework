using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Global.Telemetry.Tests.Transport
{
    // backend/ClientTelemetry.TestHost, started by tools/scripts/test-telemetry-transport.sh: its address and token are in the environment.
    public static class TelemetryTransportHost
    {
        public const string MetaRoute = "/test-host/meta";
        public const string MuteRoute = "/test-host/mute";

        private static readonly string _address = Environment.GetEnvironmentVariable("CLIENT_TELEMETRY_HOST")?.TrimEnd('/');
        private static readonly HttpClient _http = CreateClient();

        public static bool IsPresent => string.IsNullOrEmpty(_address) == false;

        public static string Socket(string route)
        {
            return "ws" + _address.Substring("http".Length) + route;
        }

        public static async UniTask<JToken> Read(string query)
        {
            var text = await _http.GetStringAsync(_address + "/api/client-telemetry/" + query).AsUniTask();

            await UniTask.SwitchToMainThread();

            return JToken.Parse(text);
        }

        public static async UniTask SetFlag(bool isEnabled)
        {
            var response = await _http.PostAsync($"{_address}/test-host/flag/{(isEnabled ? "true" : "false")}", null).AsUniTask();

            await UniTask.SwitchToMainThread();
            response.EnsureSuccessStatusCode();
        }

        public static async UniTask<long> MessagesCount()
        {
            var text = await _http.GetStringAsync(_address + "/test-host/stats").AsUniTask();

            await UniTask.SwitchToMainThread();

            return JToken.Parse(text).Value<long>("messages");
        }

        // The row of the one session with this tag, null while there is none.
        public static async UniTask<JToken> Session(string tag)
        {
            var rows = (JArray)await Read("sessions?limit=100&tag=" + tag);

            if (rows.Count > 1)
                throw new InvalidOperationException($"{rows.Count} sessions with the tag {tag}, expected one");

            return rows.Count == 0 ? null : rows[0];
        }

        // Every page of samples, errors or marks of a session.
        public static async UniTask<JArray> ReadAll(string sessionId, string list)
        {
            var result = new JArray();
            var after = 0L;

            while (true)
            {
                var page = await Read($"sessions/{sessionId}/{list}?limit=1000&after={after}");
                var items = (JArray)page["items"];

                if (items.Count == 0)
                    return result;

                foreach (var item in items)
                    result.Add(item);

                after = page.Value<long>("lastSeq");
            }
        }

        // A condition over the API, which sees the data some time after the client has sent it.
        public static async UniTask<JToken> Until(Func<UniTask<JToken>> read, Func<JToken, bool> isReady, string what, float seconds = 15f)
        {
            var left = TimeSpan.FromSeconds(seconds);
            JToken last;

            do
            {
                last = await read();

                if (last != null && isReady(last))
                    return last;

                await UniTask.Delay(100, DelayType.Realtime);
                left -= TimeSpan.FromMilliseconds(100);
            } while (left > TimeSpan.Zero);

            throw new TimeoutException($"Not in {seconds} s: {what}. Last read: {last}");
        }

        public static UniTask<JToken> UntilClosed(string tag, string status)
        {
            return Until(() => Session(tag), row => row.Value<string>("status") != "live", $"the session {tag} closed")
                .ContinueWith(row => row.Value<string>("status") == status
                    ? row
                    : throw new InvalidOperationException($"The session {tag} closed as {row.Value<string>("status")}, expected {status}"));
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            var token = Environment.GetEnvironmentVariable("CLIENT_TELEMETRY_TOKEN");

            if (string.IsNullOrEmpty(token) == false)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }
    }
}
