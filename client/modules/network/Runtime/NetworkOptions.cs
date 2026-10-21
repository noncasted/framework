using UnityEngine;

namespace Internal
{
    public class NetworkOptions : EnvAsset
    {
        // How many times per second changed properties of network objects are sent to the server.
        [SerializeField, Min(1f)] private float _propertiesSyncRate = 60f;

        public float PropertiesSyncInterval => 1f / _propertiesSyncRate;
    }
}
