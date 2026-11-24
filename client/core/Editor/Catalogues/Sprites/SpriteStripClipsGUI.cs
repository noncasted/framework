using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The clips of a PNG strip marked as Multiple Animations: a row per clip - its name, frames and time. All edits go
    // through apply and reach the disk on the importer's Apply.
    public static class SpriteStripClipsGUI
    {
        private const float FramesWidth = 120f;
        private const float TimeWidth = 50f;
        private const float RemoveButtonWidth = 22f;

        public static void Draw(
            IReadOnlyList<AssetImporter> importers,
            Action<IReadOnlyList<AssetImporter>, Action<SpriteCatalogMetadata>> apply)
        {
            if (importers.Count != 1)
            {
                EditorGUILayout.HelpBox("Select a single file to edit its clips.", MessageType.Info);
                return;
            }

            var importer = importers[0];
            var metadata = SpriteCatalogMetadata.ReadOrDefault(importer);
            var count = CountOf(importer.assetPath, null);

            EditorGUILayout.LabelField("Clips", $"{count} frames in the strip");

            if (metadata.Clips.Count == 0)
                EditorGUILayout.HelpBox("No clips: the generation fails until one is added.", MessageType.Warning);

            for (var i = 0; i < metadata.Clips.Count; i++)
                DrawClip(importers, apply, metadata.Clips[i], i, file => CountOf(importer.assetPath, file));

            if (GUILayout.Button("Add clip"))
                apply(importers, next => next.AddClip(new SpriteCatalogClip($"Clip{next.Clips.Count}", string.Empty,
                    SpriteCatalogMetadata.DefaultTime)));
        }

        private static void DrawClip(
            IReadOnlyList<AssetImporter> importers,
            Action<IReadOnlyList<AssetImporter>, Action<SpriteCatalogMetadata>> apply,
            SpriteCatalogClip clip,
            int index,
            Func<string, int> count)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            var name = EditorGUILayout.DelayedTextField(clip.Name);
            var frames = EditorGUILayout.DelayedTextField(clip.Frames, GUILayout.Width(FramesWidth));
            var time = EditorGUILayout.DelayedFloatField(clip.Time, GUILayout.Width(TimeWidth));

            if (EditorGUI.EndChangeCheck())
                apply(importers, next => next.SetClip(index, new SpriteCatalogClip(name, frames, Mathf.Max(time, 0.01f))));

            var remove = GUILayout.Button("x", GUILayout.Width(RemoveButtonWidth));
            EditorGUILayout.EndHorizontal();

            if (SpriteClipFrames.TryParse(clip.Frames, count, out _, out var error) == false)
                EditorGUILayout.HelpBox(error, MessageType.Error);

            if (remove)
                apply(importers, next => next.RemoveClipAt(index));
        }

        private static int CountOf(string assetPath, string file)
        {
            var path = file == null ? assetPath : SpriteStripClips.SiblingPath(assetPath, file);
            return SpriteSheetLoader.CollectSprites(path, SpriteKind.Animation).Length;
        }
    }
}
