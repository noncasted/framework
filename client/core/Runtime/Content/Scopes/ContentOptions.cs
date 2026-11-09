using System;
using UnityEngine;

namespace Internal
{
    // The content directory of the game for AddContent: its name (the folder StreamingAssets/Content/<Name> and the name of
    // its index, the Name of the Content section of ProjectSettings/Framework.json) and, for a WebGL player that takes the
    // files from a CDN, the url of the folder of the content folders (empty: StreamingAssets of the page).
    [Serializable]
    public class ContentOptions
    {
        [SerializeField] private string _name;
        [SerializeField] private string _remoteUrl;

        public string Name => _name;

        public string RemoteUrl => string.IsNullOrEmpty(_remoteUrl) ? null : _remoteUrl;

        public void Validate()
        {
            if (string.IsNullOrEmpty(_name))
                throw new InvalidOperationException("[Content] ContentOptions without the name of the content directory");
        }
    }
}
