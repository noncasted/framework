using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    public abstract class SpriteGroup : AssetGroup<SpriteGroupAsset>
    {
        // A sheet of the loaded group by its name: for content that names its sprites in data (folder icons by index)
        // instead of the generated properties.
        public Sprite GetSheet(string name)
        {
            if (Asset == null)
                throw new InvalidOperationException($"{Name} is not loaded: {name} is asked before the group is retained");

            return Asset.GetSheet(name);
        }

        // Every sheet of the loaded group in the order of its names: for content that picks a sprite at random or
        // walks the whole group (backgrounds of a graph, images of a sound layer).
        public IReadOnlyList<Sprite> Sheets
        {
            get
            {
                if (Asset == null)
                    throw new InvalidOperationException(
                            $"{Name} is not loaded: its sheets are asked before the group is retained");

                return Asset.Sheets;
            }
        }

        // An animation of the loaded group by its full name (<aseprite file><tag>): for content that names its clips in
        // data (enemy skins) instead of the generated properties.
        public ISpriteAnimationData GetAnimation(string name)
        {
            if (Asset == null)
                throw new InvalidOperationException($"{Name} is not loaded: {name} is asked before the group is retained");

            return Asset.GetAnimation(name);
        }

        public bool HasAnimation(string name)
        {
            if (Asset == null)
                throw new InvalidOperationException($"{Name} is not loaded: {name} is asked before the group is retained");

            return Asset.HasAnimation(name);
        }
    }
}
