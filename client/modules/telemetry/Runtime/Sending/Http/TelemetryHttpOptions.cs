using System;

namespace Global.Telemetry
{
    // Built by the game in its scope. BaseUrl: the prefix of /start, /batch, /end, /startup, a path of the server of the page on
    // the web; outside the web an absolute url (a relative one fails every request there). TimeoutSeconds: one request.
    public class TelemetryHttpOptions
    {
        public TelemetryHttpOptions(string baseUrl = "/api/telemetry", float timeoutSeconds = 10f)
        {
            if (string.IsNullOrEmpty(baseUrl) || baseUrl.EndsWith("/"))
                throw new ArgumentException("[Telemetry] The base url must be set, without a trailing slash", nameof(baseUrl));

            if (timeoutSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "[Telemetry] Must be above zero");

            BaseUrl = baseUrl;
            TimeoutSeconds = timeoutSeconds;
        }

        public string BaseUrl { get; }
        public float TimeoutSeconds { get; }
    }
}
