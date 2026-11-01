using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class DebugOptions
    {
        [SerializeField] private bool _enableGizmos;
        [SerializeField] private bool _enableLogs;

        public bool EnableGizmos => _enableGizmos;

        public bool EnableLogs => _enableLogs;
    }
}