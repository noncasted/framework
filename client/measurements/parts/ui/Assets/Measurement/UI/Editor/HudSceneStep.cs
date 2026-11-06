using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Measurement
{
    /// <summary>
    ///     Adds the canvas of <see cref="HudPart" />. The font is a static SDF atlas, as in the games: the native
    ///     font engine is stripped (TextCoreFontEngine, Freetype), so a dynamic font would draw nothing.
    /// </summary>
    public sealed class HudSceneStep : IMeasurementSceneStep
    {
        // A copy of the font of TextMeshPro (OFL): its place in the uGUI package differs between Unity versions.
        private const string FontPath = "Assets/Measurement/UI/Fonts/LiberationSans.ttf";
        private const string SdfShader = "TextMeshPro/Mobile/Distance Field";
        private const string LeadingPath = "Assets/Measurement/UI/LineBreaking/Leading.txt";
        private const string FollowingPath = "Assets/Measurement/UI/LineBreaking/Following.txt";
        private const string Characters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        public void Build(MeasurementSceneContext context)
        {
            RequireShaders();
            var settings = CreateSettings(context.Folder);
            var font = CreateFont(context.Folder);
            SetDefaultFont(settings, font);

            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = context.Camera;
            canvas.planeDistance = 5f;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();

            var panel = CreateChild("Panel", canvasObject.transform);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(24f, -24f);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var label = CreateText("Label", panel, font, "Measurement");

            var bar = CreateChild("Bar", panel).gameObject.AddComponent<Image>();
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;

            var buttonTransform = CreateChild("Button", panel);
            buttonTransform.gameObject.AddComponent<Image>();
            var button = buttonTransform.gameObject.AddComponent<Button>();
            CreateText("Text", buttonTransform, font, "Button").color = Color.black;
            buttonTransform.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            var hud = canvasObject.AddComponent<HudPart>();
            hud.Init(label, bar, button);
            context.Run.AddPart(hud);
        }

        // uGUI 2.x of Unity 6 keeps the TextMeshPro shaders in the essential resources a project imports (the fork of
        // Unity 7000 ships them in the package): measure.py writes them into the project (the "unitypackages" override).
        private static void RequireShaders()
        {
            if (Shader.Find(SdfShader) == null)
                throw new System.InvalidOperationException($"No shader {SdfShader}: the variant does not bring the TextMeshPro shaders for this version");
        }

        // The settings of TextMeshPro, the same in every version: uGUI 2.x of Unity 6 has no settings without a project
        // asset, and the built-in ones of Unity 7000 name the dynamic LiberationSans SDF of the package as the default
        // font, which a build would take. The default font is the static one of the scene (SetDefaultFont).
        private static TMP_Settings CreateSettings(string folder)
        {
            var resources = $"{folder}/Resources";
            AssetDatabase.CreateFolder(folder, "Resources");

            var settings = ScriptableObject.CreateInstance<TMP_Settings>();
            AssetDatabase.CreateAsset(settings, $"{resources}/TMP Settings.asset");

            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_leadingCharacters").objectReferenceValue = MeasurementAssets.Load<TextAsset>(LeadingPath);
            serialized.FindProperty("m_followingCharacters").objectReferenceValue = MeasurementAssets.Load<TextAsset>(FollowingPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            return settings;
        }

        private static void SetDefaultFont(TMP_Settings settings, TMP_FontAsset font)
        {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static TMP_FontAsset CreateFont(string folder)
        {
            var path = $"{folder}/Liberation SDF.asset";
            var source = MeasurementAssets.Load<Font>(FontPath);

            var asset = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 512, 512);
            asset.name = "Liberation SDF";

            if (asset.TryAddCharacters(Characters, out var missing) == false)
                throw new System.InvalidOperationException($"The font has no glyphs for '{missing}'");

            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(asset, path);

            // As the fonts of pixel-swarm: no source font in the build. A font asset that keeps it makes the player load
            // the font file through Freetype and the font engine, which the stripping removed, and the player hangs.
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_SourceFontFile").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (var atlas in asset.atlasTextures)
            {
                atlas.name = "Liberation SDF Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }

            asset.material.name = "Liberation SDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.SaveAssets();

            return MeasurementAssets.Load<TMP_FontAsset>(path);
        }

        private static RectTransform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);

            return (RectTransform)child.transform;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string text)
        {
            var label = CreateChild(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 36f;
            label.text = text;

            return label;
        }
    }
}
