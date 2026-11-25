using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class ForwardAnimationData
    {
        [SerializeField] private Sprite[] _sprites;
        [SerializeField] private float _time = 0.8f;
        [SerializeField] private Color _color = Color.white;

        public IReadOnlyList<Sprite> Sprites => _sprites;
        public float Time => _time;
        public Color Color => _color;
    }
}
