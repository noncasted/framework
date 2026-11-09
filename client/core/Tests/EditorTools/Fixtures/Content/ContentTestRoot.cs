using Unity.Loading;
using UnityEngine;

namespace Internal.EditorTests.Content
{
    // The root of the content directory of the index tests: only a lazy reference to an asset and the id of a scene.
    public sealed class ContentTestRoot : ScriptableObject
    {
        [SerializeField] private Loadable<ContentTestTarget> _target;
        [SerializeField] private LoadableSceneId _scene;

        public void Init(LoadableObjectId target, LoadableSceneId scene)
        {
            _target = new Loadable<ContentTestTarget>(target);
            _scene = scene;
        }
    }
}
