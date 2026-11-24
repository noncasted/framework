using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Internal
{
    // The animations of a PNG strip marked as Multiple Animations: one per clip of its markup, made of the sprites of the
    // strip (and of the strips of its folder a clip names) in the order of their sheets. Nothing is baked: the clips
    // reference the sprites of the files themselves.
    internal static class SpriteStripClips
    {
        internal static List<BakedAnimation> Cut(string assetPath, SpriteCatalogMetadata metadata)
        {
            var strips = new Dictionary<string, Sprite[]>(StringComparer.Ordinal);

            return Cut(assetPath, file =>
            {
                var path = file == null ? assetPath : SiblingPath(assetPath, file);

                if (strips.TryGetValue(path, out var strip) == false)
                {
                    strip = SpriteSheetLoader.CollectSprites(path, SpriteKind.Animation);
                    strips.Add(path, strip);
                }

                return strip;
            }, metadata.Clips);
        }

        // The strip of another file of the folder by its name, with the extension of the strip that names it.
        internal static string SiblingPath(string assetPath, string file)
        {
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            return $"{folder}/{file}{Path.GetExtension(assetPath)}";
        }

        // The strips by the name of the file (null - the strip itself); a missing one is empty.
        internal static List<BakedAnimation> Cut(
            string assetPath,
            Func<string, IReadOnlyList<Sprite>> strips,
            IReadOnlyList<SpriteCatalogClip> clips)
        {
            if (clips.Count == 0)
                throw new InvalidOperationException($"{assetPath} is marked as Multiple Animations and has no clips");

            var names = new HashSet<string>(StringComparer.Ordinal);
            var animations = new List<BakedAnimation>(clips.Count);

            foreach (var clip in clips)
            {
                if (string.IsNullOrWhiteSpace(clip.Name))
                    throw new InvalidOperationException($"A clip of {assetPath} has no name");

                if (names.Add(clip.Name) == false)
                    throw new InvalidOperationException($"{assetPath} has two clips {clip.Name}");

                if (clip.Time <= 0f)
                    throw new InvalidOperationException($"The clip {clip.Name} of {assetPath} has the time {clip.Time}");

                if (SpriteClipFrames.TryParse(clip.Frames, file => strips(file).Count, out var frames, out var error) ==
                    false)
                    throw new InvalidOperationException($"The clip {clip.Name} of {assetPath}: {error}");

                var sprites = new Sprite[frames.Count];

                for (var i = 0; i < frames.Count; i++)
                    sprites[i] = strips(frames[i].File)[frames[i].Index];

                animations.Add(new BakedAnimation(clip.Name, sprites, clip.Time));
            }

            return animations;
        }
    }
}
