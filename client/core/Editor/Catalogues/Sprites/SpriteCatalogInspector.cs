using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [InitializeOnLoad]
    public static class SpriteCatalogInspector
    {
        static SpriteCatalogInspector()
        {
            Editor.finishedDefaultHeaderGUI += Draw;
        }

        private static string ArtPrefix => FrameworkSettings.ArtFolder + "/";

        private static void Draw(Editor editor)
        {
            if (CatalogInspectorGUI.TryCollectImporters(editor, IsCatalogTarget, out var importers) == false)
                return;

            using (CatalogInspectorGUI.BeginSection())
            {
                var states = ResolveStates(importers);
                DrawIncluded(importers, states);

                if (states.Included == false || states.IncludedMixed)
                    return;

                DrawKind(importers, states);

                CatalogGroupField.Draw(
                    SpriteGroupsRegistry.Instance,
                    states.Group,
                    states.GroupMixed,
                    group => Apply(importers, metadata => metadata.Group = group));

                if (states.KindMixed)
                    return;

                if (states.Kind == SpriteCatalogKind.Animation)
                {
                    DrawTime(importers, states);
                    DrawColor(importers, states);
                }
                else if (states.Kind == SpriteCatalogKind.MultipleAnimations)
                {
                    DrawFramesPerSecond(importers, states);
                    DrawColor(importers, states);
                    SpriteMultipleAnimationsGUI.Draw(importers, Apply);
                }
            }
        }

        private static bool IsCatalogTarget(AssetImporter importer)
        {
            if (string.IsNullOrEmpty(importer.assetPath))
                return false;

            if (importer.assetPath.StartsWith(ArtPrefix, StringComparison.OrdinalIgnoreCase) == false)
                return false;

            return SpriteGenerator.IsSourceFile(importer.assetPath);
        }

        private static CatalogStates ResolveStates(IReadOnlyList<AssetImporter> importers)
        {
            var first = SpriteCatalogMetadata.ReadOrDefault(importers[0]);
            var includedMixed = false;
            var kindMixed = false;
            var groupMixed = false;
            var timeMixed = false;
            var colorMixed = false;
            var framesPerSecondMixed = false;

            for (var i = 1; i < importers.Count; i++)
            {
                var metadata = SpriteCatalogMetadata.ReadOrDefault(importers[i]);

                if (metadata.Included != first.Included)
                    includedMixed = true;

                if (metadata.Kind != first.Kind)
                    kindMixed = true;

                if (string.Equals(metadata.Group, first.Group, StringComparison.Ordinal) == false)
                    groupMixed = true;

                if (Mathf.Approximately(metadata.Time, first.Time) == false)
                    timeMixed = true;

                if (metadata.Color != first.Color)
                    colorMixed = true;

                if (Mathf.Approximately(metadata.FramesPerSecond, first.FramesPerSecond) == false)
                    framesPerSecondMixed = true;
            }

            return new CatalogStates(
                first.Included,
                first.Kind,
                first.Group,
                first.Time,
                first.Color,
                first.FramesPerSecond,
                includedMixed,
                kindMixed,
                groupMixed,
                timeMixed,
                colorMixed,
                framesPerSecondMixed,
                AllAseprite(importers));
        }

        private static void DrawIncluded(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            if (CatalogInspectorGUI.TryDrawToggle("Sprite Catalog", states.Included, states.IncludedMixed,
                out var included))
                Apply(importers, metadata => metadata.Included = included);
        }

        private static void DrawKind(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Kind");

            var isSheet = states.KindMixed == false && states.Kind == SpriteCatalogKind.Sheet;
            var isAnimation = states.KindMixed == false && states.Kind == SpriteCatalogKind.Animation;
            var isMultiple = states.KindMixed == false && states.Kind == SpriteCatalogKind.MultipleAnimations;

            var nextSheet = GUILayout.Toggle(isSheet, "Sheet", EditorStyles.radioButton);
            var nextAnimation = GUILayout.Toggle(isAnimation, "Animation", EditorStyles.radioButton);
            var nextMultiple = false;

            // Only aseprite has tags and layers.
            if (states.AllAseprite)
                nextMultiple = GUILayout.Toggle(isMultiple, "Multiple Animations", EditorStyles.radioButton);

            EditorGUILayout.EndHorizontal();

            if (nextSheet && isSheet == false)
                Apply(importers, metadata => metadata.Kind = SpriteCatalogKind.Sheet);
            else if (nextAnimation && isAnimation == false)
                Apply(importers, SwitchToAnimation);
            else if (nextMultiple && isMultiple == false)
                Apply(importers, metadata => metadata.Kind = SpriteCatalogKind.MultipleAnimations);
        }

        private static void DrawFramesPerSecond(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            var changed = CatalogInspectorGUI.TryDraw(
                states.FramesPerSecondMixed,
                () => EditorGUILayout.FloatField("FPS", states.FramesPerSecond),
                out var framesPerSecond);

            if (changed)
                Apply(importers, metadata => metadata.FramesPerSecond = Mathf.Max(framesPerSecond, 0.01f));
        }

        private static bool AllAseprite(IReadOnlyList<AssetImporter> importers)
        {
            foreach (var importer in importers)
            {
                if (SpriteCatalogMetadata.IsAsepriteImporter(importer) == false)
                    return false;
            }

            return true;
        }

        private static void DrawTime(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            var changed = CatalogInspectorGUI.TryDraw(
                states.TimeMixed,
                () => EditorGUILayout.FloatField("Time", states.Time),
                out var time);

            if (changed)
                Apply(importers, metadata => metadata.Time = time);
        }

        private static void DrawColor(IReadOnlyList<AssetImporter> importers, CatalogStates states)
        {
            var changed = CatalogInspectorGUI.TryDraw(
                states.ColorMixed,
                () => EditorGUILayout.ColorField("Color", states.Color),
                out var color);

            if (changed)
                Apply(importers, metadata => metadata.Color = color);
        }

        // Changes live in the importer's memory until Apply in the inspector; the reimport on Apply
        // triggers generation via SpriteCatalogPostprocessor, Revert discards them.
        private static void Apply(IReadOnlyList<AssetImporter> importers, Action<SpriteCatalogMetadata> mutate)
        {
            foreach (var importer in importers)
            {
                Undo.RecordObject(importer, "Sprite Catalog");
                var metadata = SpriteCatalogMetadata.ReadOrDefault(importer);
                mutate(metadata);

                if (metadata.Included && string.IsNullOrEmpty(metadata.Group))
                    metadata.Group = SpriteCatalogMetadata.GetDefaultGroup(importer.assetPath);

                SpriteCatalogMetadata.Stage(importer, metadata);
            }
        }

        private static void SwitchToAnimation(SpriteCatalogMetadata metadata)
        {
            if (metadata.Kind != SpriteCatalogKind.Animation)
            {
                metadata.Time = SpriteCatalogMetadata.DefaultTime;
                metadata.Color = SpriteCatalogMetadata.DefaultColor;
            }

            metadata.Kind = SpriteCatalogKind.Animation;
        }

        private readonly struct CatalogStates
        {
            public CatalogStates(
                bool included,
                SpriteCatalogKind kind,
                string group,
                float time,
                Color color,
                float framesPerSecond,
                bool includedMixed,
                bool kindMixed,
                bool groupMixed,
                bool timeMixed,
                bool colorMixed,
                bool framesPerSecondMixed,
                bool allAseprite)
            {
                Included = included;
                Kind = kind;
                Group = group;
                Time = time;
                Color = color;
                FramesPerSecond = framesPerSecond;
                IncludedMixed = includedMixed;
                KindMixed = kindMixed;
                GroupMixed = groupMixed;
                TimeMixed = timeMixed;
                ColorMixed = colorMixed;
                FramesPerSecondMixed = framesPerSecondMixed;
                AllAseprite = allAseprite;
            }

            public bool Included { get; }
            public SpriteCatalogKind Kind { get; }
            public string Group { get; }
            public float Time { get; }
            public Color Color { get; }
            public float FramesPerSecond { get; }
            public bool IncludedMixed { get; }
            public bool KindMixed { get; }
            public bool GroupMixed { get; }
            public bool TimeMixed { get; }
            public bool ColorMixed { get; }
            public bool FramesPerSecondMixed { get; }
            public bool AllAseprite { get; }
        }
    }
}