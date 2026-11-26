using System;
using UnityEngine;

namespace Internal.EditorTests.Content
{
    // The part of ContentTestManaged: a class no code of a player references, only a [SerializeReference] of a bundle.
    [Serializable]
    public sealed class ContentTestPart
    {
        [SerializeField] private int _value;

        public int Value => _value;

        public void Init(int value)
        {
            _value = value;
        }
    }
}
