using UnityEngine;

namespace Internal.EditorTests.Content
{
    // A root of the content directory of the loader tests: a content reference to the target and a content scene.
    public sealed class ContentTestReferences : ScriptableObject
    {
        [SerializeField] private ContentReference<ContentTestTarget> _target;
        [SerializeField] private ContentScene _scene;

        public ContentReference<ContentTestTarget> Target => _target;
        public ContentScene Scene => _scene;

        public void Init(ContentReference<ContentTestTarget> target, ContentScene scene)
        {
            _target = target;
            _scene = scene;
        }
    }
}
