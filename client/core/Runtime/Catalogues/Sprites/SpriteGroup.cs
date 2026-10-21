using System;

namespace Internal
{
    public abstract class SpriteGroup : AssetGroup<SpriteGroupAsset>
    {
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
