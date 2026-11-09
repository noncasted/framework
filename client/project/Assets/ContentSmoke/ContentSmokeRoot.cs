using Internal;
using UnityEngine;

namespace ContentSmoke
{
    // The root of the content directory of client/project: a lazy reference to an asset and a scene, enough for a player
    // build to build, check and index a directory.
    public sealed class ContentSmokeRoot : ScriptableObject
    {
        [SerializeField] private ContentReference<ContentSmokeTarget> _target;
        [SerializeField] private ContentScene _scene;

        public ContentReference<ContentSmokeTarget> Target => _target;
        public ContentScene Scene => _scene;
    }
}
