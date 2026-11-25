using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class BackendOptions
    {
        [SerializeField] private BackendEnvironment _environment;
        [SerializeField] private string _productionApiUrl = "";
        [SerializeField] private string _localApiUrl = "http://localhost:5003";
        [SerializeField] private int _timeoutSeconds = 10;

        public BackendEnvironment Environment => _environment;

        public string ProductionApiUrl => _productionApiUrl;

        public string LocalApiUrl => _localApiUrl;

        // The ceiling of waiting for a response: without it a client would hang on an unreachable server instead of
        // going offline.
        public int TimeoutSeconds => _timeoutSeconds;

        // The backend this client talks to: the chosen one in the editor, production in a build (see Url).
        public BackendEnvironment Target => Application.isEditor == false ? BackendEnvironment.Production : _environment;

        public string Url
        {
            get
            {
                if (Application.isEditor == false)
                    return _productionApiUrl;

                return _environment switch
                {
                    BackendEnvironment.Local => _localApiUrl,
                    BackendEnvironment.Production => _productionApiUrl,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        // The socket of the same backend: only the scheme of Url changes (http -> ws, https -> wss), whichever
        // environment is targeted.
        public string SocketUrl
        {
            get
            {
                var url = Url;

                if (url.StartsWith("https://", StringComparison.Ordinal) == true)
                    return "wss://" + url.Substring("https://".Length);

                if (url.StartsWith("http://", StringComparison.Ordinal) == true)
                    return "ws://" + url.Substring("http://".Length);

                throw new InvalidOperationException("The backend url has no http or https scheme: " + url);
            }
        }
    }
}