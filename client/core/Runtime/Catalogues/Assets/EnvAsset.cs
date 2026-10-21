using UnityEngine;

namespace Internal
{
    // Marker of an asset that goes into the catalogue. It carries no state of its own:
    // the asset's identity is its group and its name in the catalogue.
    public abstract class EnvAsset : ScriptableObject
    {
    }
}