using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Internal
{
    // The YAML of the games holds the old keys: FormerlySerializedAs stays until every game has re-saved its assets.
    // The keys are for SerializedProperty access from the generators and the tests.
    [Serializable]
    public sealed class EnvAssetEntry
    {
        public const string NameKey = nameof(_name);
        public const string AssetKey = nameof(_asset);

        [SerializeField] [FormerlySerializedAs("Name")] private string _name;
        [SerializeField] [FormerlySerializedAs("Asset")] private EnvAsset _asset;

        public string Name => _name;
        public EnvAsset Asset => _asset;
    }
}
