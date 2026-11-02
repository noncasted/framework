using UnityEngine;

namespace Internal
{
    public sealed class BakedAnimation
    {
        public BakedAnimation(string name, Sprite[] sprites, float time)
        {
            Name = name;
            Sprites = sprites;
            Time = time;
        }

        public string Name { get; }
        public Sprite[] Sprites { get; }
        public float Time { get; }
    }
}
