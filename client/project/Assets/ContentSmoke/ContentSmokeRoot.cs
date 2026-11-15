using Internal;
using UnityEngine;

namespace ContentSmoke
{
    // The root of the content bundle Smoke of client/project: a lazy reference to an asset and a scene, enough for a player
    // build to lay out, check and build a bundle.
    public sealed class ContentSmokeRoot : ScriptableObject
    {
        [SerializeField] private ContentReference<ContentSmokeTarget> _target;
        [SerializeField] private ContentScene _scene;

        public ContentReference<ContentSmokeTarget> Target => _target;
        public ContentScene Scene => _scene;
    }
}
