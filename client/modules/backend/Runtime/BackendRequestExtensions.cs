namespace Global.Backend
{
    public static class BackendRequestExtensions
    {
        // The headers of the request and the timeout of BackendOptions: without a timeout a request to a silent
        // server waited forever.
        public static void SetHeaders(
            this UnityEngine.Networking.UnityWebRequest webRequest,
            System.Collections.Generic.IReadOnlyList<IRequestHeader> headers,
            Internal.BackendOptions options)
        {
            if (options.TimeoutSeconds <= 0)
                throw new System.InvalidOperationException($"[Backend] The timeout of the requests is {options.TimeoutSeconds} s");

            webRequest.timeout = options.TimeoutSeconds;

            foreach (var header in headers)
                webRequest.SetRequestHeader(header.Type, header.Value);
        }
    }
}
