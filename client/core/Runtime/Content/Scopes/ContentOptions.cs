using System;
using UnityEngine;

namespace Internal
{
    // The content directory of the game for AddContent: its name (the folder StreamingAssets/Content/<Name> and the name of
    // its index, the Name of the Content section of ProjectSettings/Framework.json) and, for a WebGL player that takes the
    // files from a CDN, the url of the folder of the content folders (empty: StreamingAssets of the page), and how many
    // of its files a WebGL player fetches at once.
    [Serializable]
    public class ContentOptions
    {
        public const int DefaultMaxFetches = 64;

        [SerializeField] private string _name;
        [SerializeField] private string _remoteUrl;

        // A level needs thousands of files: started at once they exhaust the browser (net::ERR_INSUFFICIENT_RESOURCES).
        // 64 keeps a link of 10 Mbit with 150 ms of latency busy with files of a few KB (HTTP/2); the files a load asks
        // for later wait in the order they were asked for. The files the Cache API of the page holds are not counted.
        [SerializeField] [Min(1)] private int _maxFetches = DefaultMaxFetches;

        // Serialized by a scene or an asset of the game.
        public ContentOptions()
        {
        }

        // A directory named in code (a smoke player, a test).
        public ContentOptions(string name, string remoteUrl = null, int maxFetches = DefaultMaxFetches)
        {
            _name = name;
            _remoteUrl = remoteUrl;
            _maxFetches = maxFetches;
        }

        public string Name => _name;

        public string RemoteUrl => string.IsNullOrEmpty(_remoteUrl) ? null : _remoteUrl;

        public int MaxFetches => _maxFetches;

        public void Validate()
        {
            if (string.IsNullOrEmpty(_name))
                throw new InvalidOperationException("[Content] ContentOptions without the name of the content directory");

            if (_maxFetches < 1)
                throw new InvalidOperationException($"[Content] ContentOptions of {_name}: MaxFetches {_maxFetches}, at least one fetch must run");
        }
    }
}
