using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class DebugOptions
    {
        [SerializeField] private bool _enableGizmos;

        public bool EnableGizmos => _enableGizmos;
    }
}