using Unity.Loading;
using UnityEngine;

namespace ContentSmoke
{
    // The root of the content directory of client/project: a lazy reference to an asset and the id of a scene, enough
    // for a player build to build, check and index a directory.
    public sealed class ContentSmokeRoot : ScriptableObject
    {
        [SerializeField] private Loadable<ContentSmokeTarget> _target;
        [SerializeField] private LoadableSceneId _scene;

        public Loadable<ContentSmokeTarget> Target => _target;
        public LoadableSceneId Scene => _scene;
    }
}
