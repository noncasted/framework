using System;
using UnityEngine;

namespace Internal
{
    // The content of the game for AddContent: the name of its main bundle (the ContentBundle asset whose roots hold the
    // ContentRoot of the catalogues) and, for a WebGL player that takes the bundles from a CDN, the url of the folder of the
    // Content folder (empty: StreamingAssets of the page) and how many requests it sends at once.
    [Serializable]
    public class ContentOptions
    {
        public const int DefaultMaxFetches = 64;

        [SerializeField] private string _name;
        [SerializeField] private string _remoteUrl;

        // Too many requests at once exhaust the browser (net::ERR_INSUFFICIENT_RESOURCES); the bundles a load asks for
        // later wait in the order they were asked for. The files the Cache API of the page holds are not counted.
        [SerializeField] [Min(1)] private int _maxFetches = DefaultMaxFetches;

        // Serialized by a scene or an asset of the game.
        public ContentOptions()
        {
        }

        // A bundle named in code (a smoke player, a test).
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
                throw new InvalidOperationException("[Content] ContentOptions without the name of the main content bundle");

            if (_maxFetches < 1)
                throw new InvalidOperationException($"[Content] ContentOptions of {_name}: MaxFetches {_maxFetches}, at least one fetch must run");
        }
    }
}
