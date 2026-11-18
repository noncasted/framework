using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // A content bundle of the project, the unit a player fetches and loads: its roots (assets and scenes) and everything
    // they reach, by direct references and by lazy ones (ContentReference, ContentScene), except what another bundle
    // holds (ContentBundleLayout). The name of the asset is the name of the bundle (IContentLoadersCollection, ContentOptions.Name).
    // A preloaded bundle goes into the .data file of a WebGL build; the others are fetched by a WebGL player when first
    // needed, in gzip with IsGzip (unpacked by the page: for a host that compresses no binary file). Every other player
    // reads them from its StreamingAssets.
    [CreateAssetMenu(menuName = "Framework/Content Bundle", fileName = "ContentBundle")]
    public sealed class ContentBundle : ScriptableObject
    {
        [SerializeField] private List<Object> _roots = new();
        [SerializeField] private bool _isPreloaded;
        [SerializeField] private bool _isGzip;

        public string Name => name;
        public IReadOnlyList<Object> Roots => _roots;
        public bool IsPreloaded => _isPreloaded;
        public bool IsGzip => _isGzip;

        // For the tools and the tests that make bundles in code.
        public void Init(IReadOnlyList<Object> roots, bool isPreloaded, bool isGzip)
        {
            _roots = new List<Object>(roots);
            _isPreloaded = isPreloaded;
            _isGzip = isGzip;
        }
    }
}
