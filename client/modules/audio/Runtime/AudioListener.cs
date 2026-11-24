using Internal;
using UnityEngine;

namespace Global.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.AudioListener))]
    public class AudioListener :
        MonoBehaviour,
        IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            GetComponent<UnityEngine.AudioListener>().enabled = true;
        }
    }
}