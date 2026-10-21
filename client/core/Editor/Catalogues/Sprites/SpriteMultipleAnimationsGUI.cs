using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Multiple Animations block: animations from aseprite tags with layer selection, and custom
    // animations on top of tags. All edits go through apply and reach the disk on the importer's Apply.
    public static class SpriteMultipleAnimationsGUI
    {
        private const float NameWidth = 110f;
        private const float RemoveButtonWidth = 22f;
        private const float ToggleExtraWidth = 22f;
        private const float SpeedWidth = 40f;

        public static void Draw(
            IReadOnlyList<AssetImporter> importers,
            Action<IReadOnlyList<AssetImporter>, Action<SpriteCatalogMetadata>> apply)
        {
            if (importers.Count != 1)
            {
                EditorGUILayout.HelpBox("Select a single file to edit its animations.", MessageType.Info);
                return;
            }

            var importer = importers[0];
            var document = AsepriteDocument.ReadCached(importer.assetPath);

            if (document == null)
            {
                EditorGUILayout.HelpBox("Failed to read the aseprite file.", MessageType.Error);
                return;
            }

            var metadata = SpriteCatalogMetadata.ReadOrDefault(importer);

            EditorGUILayout.Space(4f);
            DrawAnimations(importers, apply, document, metadata);
            EditorGUILayout.Space(4f);
            DrawCustomAnimations(importers, apply, document, metadata);
        }

        private static void DrawAnimations(
            IReadOnlyList<AssetImporter> importers,
            Action<IReadOnlyList<AssetImporter>, Action<SpriteCatalogMetadata>> apply,
            AsepriteDocument document,
            SpriteCatalogMetadata metadata)
        {
            EditorGUILayout.LabelField("Animations", EditorStyles.boldLabel);

            if (document.Tags.Count == 0)
            {
                EditorGUILayout.HelpBox("The file has no tags.", MessageType.Warning);
                return;
            }

            foreach (var tag in document.Tags)
            {
                var layers = metadata.FindAnimation(tag.Name)?.Layers ?? document.GetVisibleLayerNames();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(tag.Name, GUILayout.Width(NameWidth));

                if (TryDrawLayers(document, layers, out var layer, out var enabled))
                {
                    var tagName = tag.Name;
                    apply(importers, next => SetLayer(GetOrCreateAnimation(next, document, tagName).Layers,
                        layer, enabled));
                }

                GUILayout.FlexibleSpace();

                // FPS of the tag: the one of the file until it is changed here.
                EditorGUI.BeginChangeCheck();
                var framesPerSecond = EditorGUILayout.DelayedFloatField(
                    metadata.GetFramesPerSecond(tag.Name),
                    GUILayout.Width(SpeedWidth));

                if (EditorGUI.EndChangeCheck())
                {
                    var speedTag = tag.Name;
                    apply(importers, next => next.SetFramesPerSecond(speedTag, Mathf.Max(framesPerSecond, 0.01f)));
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawCustomAnimations(
            IReadOnlyList<AssetImporter> importers,
            Action<IReadOnlyList<AssetImporter>, Action<SpriteCatalogMetadata>> apply,
            AsepriteDocument document,
            SpriteCatalogMetadata metadata)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Custom Animations", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(document.Tags.Count == 0))
            {
                if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(50f)))
                    apply(importers, next => next.CustomAnimations.Add(CreateCustom(next, document)));
            }

            EditorGUILayout.EndHorizontal();

            var tagNames = new List<string>(document.Tags.Count);

            foreach (var tag in document.Tags)
                tagNames.Add(tag.Name);

            for (var i = 0; i < metadata.CustomAnimations.Count; i++)
            {
                var index = i;
                var custom = metadata.CustomAnimations[i];

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUI.BeginChangeCheck();
                    var name = EditorGUILayout.DelayedTextField(custom.Name, GUILayout.Width(NameWidth));

                    if (EditorGUI.EndChangeCheck())
                        apply(importers, next => next.CustomAnimations[index].Name = name.Trim());

                    var sourceIndex = tagNames.IndexOf(custom.Source);

                    // A missing tag is shown as is, so the source is not silently replaced.
                    var options = new List<string>(tagNames);

                    if (sourceIndex < 0)
                    {
                        options.Add($"{custom.Source} (missing)");
                        sourceIndex = options.Count - 1;
                    }

                    EditorGUI.BeginChangeCheck();
                    var nextSourceIndex = EditorGUILayout.Popup(sourceIndex, options.ToArray());

                    if (EditorGUI.EndChangeCheck() && nextSourceIndex < tagNames.Count)
                    {
                        var source = tagNames[nextSourceIndex];
                        apply(importers, next => next.CustomAnimations[index].Source = source);
                    }

                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(RemoveButtonWidth)))
                        apply(importers, next => next.CustomAnimations.RemoveAt(index));

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(NameWidth + 4f);

                    if (TryDrawLayers(document, custom.Layers, out var layer, out var enabled))
                        apply(importers, next => SetLayer(next.CustomAnimations[index].Layers, layer, enabled));

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    if (string.IsNullOrEmpty(CatalogNaming.Sanitize(custom.Name)))
                        EditorGUILayout.HelpBox("Name is empty.", MessageType.Warning);
                }
            }
        }

        private static bool TryDrawLayers(
            AsepriteDocument document,
            IReadOnlyCollection<string> selected,
            out string changedLayer,
            out bool enabled)
        {
            changedLayer = null;
            enabled = false;

            foreach (var layer in document.Layers)
            {
                var isOn = Contains(selected, layer.Name);
                var width = EditorStyles.label.CalcSize(new GUIContent(layer.Name)).x + ToggleExtraWidth;
                var next = EditorGUILayout.ToggleLeft(layer.Name, isOn, GUILayout.Width(width));

                if (next == isOn)
                    continue;

                changedLayer = layer.Name;
                enabled = next;
            }

            return changedLayer != null;
        }

        // Until a tag is touched, its layers are the file's visible layers; the first edit pins the set.
        private static SpriteCatalogAnimation GetOrCreateAnimation(
            SpriteCatalogMetadata metadata,
            AsepriteDocument document,
            string tag)
        {
            var animation = metadata.FindAnimation(tag);

            if (animation != null)
                return animation;

            animation = new SpriteCatalogAnimation { Tag = tag, Layers = document.GetVisibleLayerNames() };
            metadata.Animations.Add(animation);
            return animation;
        }

        private static SpriteCatalogCustomAnimation CreateCustom(
            SpriteCatalogMetadata metadata,
            AsepriteDocument document)
        {
            var source = document.Tags[0].Name;
            var name = CatalogNaming.Sanitize(source) + "Custom";
            var candidate = name;

            for (var suffix = 2; HasCustom(metadata, candidate); suffix++)
                candidate = name + suffix;

            return new SpriteCatalogCustomAnimation
            {
                Name = candidate,
                Source = source,
                Layers = document.GetVisibleLayerNames()
            };
        }

        private static bool HasCustom(SpriteCatalogMetadata metadata, string name)
        {
            foreach (var custom in metadata.CustomAnimations)
            {
                if (string.Equals(custom.Name, name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void SetLayer(List<string> layers, string layer, bool enabled)
        {
            layers.Remove(layer);

            if (enabled)
                layers.Add(layer);
        }

        private static bool Contains(IReadOnlyCollection<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
