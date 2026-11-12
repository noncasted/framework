using Cysharp.Threading.Tasks;

namespace Global.Telemetry
{
    // A POST of a JSON text to <TelemetryHttpOptions.BaseUrl><path>: the page fetches it on the web (a path of its own server), the
    // editor and the other platforms use System.Net.Http. No UnityWebRequest: a game may strip it. The answer is the text of a 2xx
    // response; a failure of the network, the timeout or another status throws TelemetryHttpException. Nothing is repeated.
    public interface ITelemetryHttp
    {
        UniTask<string> Post(string path, string json);
    }
}
