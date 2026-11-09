using UnityEngine;

namespace Internal.EditorTests.Content
{
    // The asset behind the Loadable of the root of the index tests: a direct reference to a clip, whose samples the
    // build writes into a resource file.
    public sealed class ContentTestTarget : ScriptableObject
    {
        [SerializeField] private AudioClip _clip;

        public void Init(AudioClip clip)
        {
            _clip = clip;
        }
    }
}
