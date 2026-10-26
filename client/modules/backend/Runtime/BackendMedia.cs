using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using UnityEngine.Networking;

namespace Global.Backend
{
    public interface IBackendMedia
    {
        UniTask<AudioClip> GetAudio(IReadOnlyLifetime lifetime, IGetRequest request, AudioType audioType);
        UniTask<Texture2D> GetImage(IReadOnlyLifetime lifetime, IGetRequest request);
    }

    public class BackendMedia : IBackendMedia
    {
        public BackendMedia(BackendOptions options)
        {
            _options = options;
        }

        private readonly BackendOptions _options;

        public async UniTask<AudioClip> GetAudio(IReadOnlyLifetime lifetime, IGetRequest request, AudioType audioType)
        {
            using var trace = GameProfiler.Concurrent($"GET audio {request.Uri}");

            using var downloadHandlerAudioClip = new DownloadHandlerAudioClip(request.Uri, audioType);
            using var webRequest = new UnityWebRequest(request.Uri, "GET", downloadHandlerAudioClip, null);

            webRequest.SetHeaders(request.Headers, _options);

            // A failure (a network error, an error status, the timeout) throws UnityWebRequestException.
            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            return downloadHandlerAudioClip.audioClip;
        }

        public async UniTask<Texture2D> GetImage(IReadOnlyLifetime lifetime, IGetRequest request)
        {
            using var trace = GameProfiler.Concurrent($"GET image {request.Uri}");

            using var downloadHandler = new DownloadHandlerTexture(true);
            using var webRequest = new UnityWebRequest(request.Uri, "GET", downloadHandler, null);

            // The headers too: the image of an authorized endpoint was refused.
            webRequest.SetHeaders(request.Headers, _options);

            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            return downloadHandler.texture;
        }
    }
}