using System.Text;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine.Networking;

namespace Global.Backend
{
    public interface IBackendPost
    {
        // The body of the response.
        UniTask<string> Post(IReadOnlyLifetime lifetime, IPostRequest request);
    }

    public class BackendPost : IBackendPost
    {
        public BackendPost(BackendOptions options)
        {
            _options = options;
        }

        private readonly BackendOptions _options;

        public async UniTask<string> Post(IReadOnlyLifetime lifetime, IPostRequest request)
        {
            using var trace = GameProfiler.Concurrent($"POST {request.Uri}");

            using var downloadHandlerBuffer = new DownloadHandlerBuffer();
            UploadHandlerRaw uploadHandler = null;

            if (request.Body != null)
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body));

            using var webRequest = new UnityWebRequest(request.Uri, "POST", downloadHandlerBuffer, uploadHandler);

            webRequest.SetHeaders(request.Headers, _options);

            // A failure (a network error, an error status, the timeout) throws UnityWebRequestException.
            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            return downloadHandlerBuffer.text;
        }
    }
}
