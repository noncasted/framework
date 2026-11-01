#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Internal
{
    // A screenshot and a text hierarchy of a prefab for agents (execute_code of MCP for Unity). The prefab is
    // instantiated into the open scene with a temporary camera (UI prefabs inside a temporary canvas, 3D ones with a
    // light), rendered into Temp/PrefabScreenshots/<name>.png and destroyed. The result is text: "OK", then
    // IMAGE_PATH, PREFAB, IS_UI, RESOLUTION lines and the hierarchy after ---HIERARCHY---, or "ERROR: ...".
    public static class PrefabScreenshot
    {
        private const int DefaultResolution = 1024;
        private const float CameraPadding = 1.3f;

        public static string Capture(string prefabPath, int resolution = DefaultResolution)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return $"ERROR: Prefab not found at '{prefabPath}'";

            var outputDir = Path.Combine(Application.dataPath, "..", "Temp", "PrefabScreenshots");
            Directory.CreateDirectory(outputDir);

            var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            var outputPath = Path.Combine(outputDir, $"{prefabName}.png");

            GameObject instance = null;
            GameObject camGo = null;
            GameObject lightGo = null;
            GameObject canvasGo = null;
            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = Vector3.zero;

                var isUI = HasComponent<RectTransform>(instance);

                camGo = new GameObject("_ScreenshotCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 1000f;

                rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 4;
                cam.targetTexture = rt;

                if (isUI)
                {
                    canvasGo = SetupUICapture(instance, cam, resolution);
                }
                else
                {
                    var bounds = Calculate3DBounds(instance);
                    lightGo = new GameObject("_ScreenshotLight");
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1f;
                    lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                    var isFlat = bounds.size.z < 0.01f;
                    if (isFlat)
                    {
                        SetupOrthographicCamera(cam, bounds);
                    }
                    else
                    {
                        SetupPerspectiveCamera(cam, bounds);
                    }
                }

                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                tex.Apply();

                var bytes = tex.EncodeToPNG();
                File.WriteAllBytes(outputPath, bytes);

                var hierarchy = BuildHierarchy(instance, 0);

                var result = new StringBuilder();
                result.AppendLine($"OK");
                result.AppendLine($"IMAGE_PATH:{outputPath}");
                result.AppendLine($"PREFAB:{prefabPath}");
                result.AppendLine($"IS_UI:{isUI}");
                result.AppendLine($"RESOLUTION:{resolution}x{resolution}");
                result.AppendLine($"---HIERARCHY---");
                result.Append(hierarchy);

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}\n{ex.StackTrace}";
            }
            finally
            {
                RenderTexture.active = null;
                if (rt != null) Object.DestroyImmediate(rt);
                if (tex != null) Object.DestroyImmediate(tex);
                if (canvasGo != null) Object.DestroyImmediate(canvasGo);
                if (instance != null) Object.DestroyImmediate(instance);
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (lightGo != null) Object.DestroyImmediate(lightGo);
            }
        }

        private static GameObject SetupUICapture(GameObject instance, Camera cam, int resolution)
        {
            var canvasGo = new GameObject("_ScreenshotCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            instance.transform.SetParent(canvasGo.transform, false);

            var instanceRect = instance.GetComponent<RectTransform>();
            if (instanceRect != null)
                instanceRect.anchoredPosition = Vector2.zero;

            Canvas.ForceUpdateCanvases();

            var elementSize = MeasureUIElement(instance);
            var maxDim = Mathf.Max(elementSize.x, elementSize.y);
            if (maxDim < 1f) maxDim = 200f;
            scaler.referenceResolution = new Vector2(maxDim * CameraPadding, maxDim * CameraPadding);

            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.transform.position = new Vector3(0, 0, -10f);
            cam.transform.LookAt(Vector3.zero);

            Canvas.ForceUpdateCanvases();

            return canvasGo;
        }

        private static Vector2 MeasureUIElement(GameObject obj)
        {
            var rect = obj.GetComponent<RectTransform>();
            if (rect != null)
            {
                var size = rect.rect.size;
                if (size.x > 1f && size.y > 1f)
                    return size;
            }

            var allRects = obj.GetComponentsInChildren<RectTransform>(true);
            var corners = new Vector3[4];
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var r in allRects)
            {
                r.GetWorldCorners(corners);
                foreach (var c in corners)
                {
                    minX = Mathf.Min(minX, c.x);
                    minY = Mathf.Min(minY, c.y);
                    maxX = Mathf.Max(maxX, c.x);
                    maxY = Mathf.Max(maxY, c.y);
                }
            }

            if (minX < float.MaxValue)
                return new Vector2(maxX - minX, maxY - minY);

            return new Vector2(200f, 200f);
        }

        private static void SetupOrthographicCamera(Camera cam, Bounds bounds)
        {
            cam.orthographic = true;
            var maxDim = Mathf.Max(bounds.size.x, bounds.size.y);
            if (maxDim < 0.001f) maxDim = 1f;
            cam.orthographicSize = maxDim * 0.5f * CameraPadding;
            cam.transform.position = bounds.center + Vector3.back * 10f;
            cam.transform.LookAt(bounds.center);
        }

        private static void SetupPerspectiveCamera(Camera cam, Bounds bounds)
        {
            cam.orthographic = false;
            cam.fieldOfView = 30f;
            var maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDim < 0.001f) maxDim = 1f;
            var distance = maxDim * CameraPadding / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var offset = new Vector3(0.3f, 0.3f, -1f).normalized * distance;
            cam.transform.position = bounds.center + offset;
            cam.transform.LookAt(bounds.center);
        }

        private static Bounds Calculate3DBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds;
            }

            return new Bounds(obj.transform.position, Vector3.one);
        }

        private static bool HasComponent<T>(GameObject obj) where T : Component
        {
            return obj.GetComponentInChildren<T>(true) != null;
        }

        private static string BuildHierarchy(GameObject obj, int depth)
        {
            var sb = new StringBuilder();
            var indent = new string(' ', depth * 2);
            var active = obj.activeSelf ? "" : " [INACTIVE]";
            sb.AppendLine($"{indent}- {obj.name}{active}");

            var components = obj.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null)
                {
                    sb.AppendLine($"{indent}  [MISSING SCRIPT]");
                    continue;
                }

                var typeName = comp.GetType().Name;
                if (typeName == "Transform" || typeName == "RectTransform") continue;

                var details = GetComponentDetails(comp, depth);
                sb.AppendLine($"{indent}  [{typeName}]{details}");
            }

            for (int i = 0; i < obj.transform.childCount; i++)
            {
                var child = obj.transform.GetChild(i).gameObject;
                sb.Append(BuildHierarchy(child, depth + 1));
            }

            return sb.ToString();
        }

        private static string GetComponentDetails(Component comp, int depth)
        {
            var sb = new StringBuilder();

            if (comp is MonoBehaviour mb)
            {
                var so = new SerializedObject(mb);
                var prop = so.GetIterator();
                var fields = new List<string>();
                prop.NextVisible(true);
                while (prop.NextVisible(false))
                {
                    if (prop.name == "m_Script") continue;
                    var val = GetPropertyValue(prop);
                    if (val != null)
                        fields.Add($"{prop.name}={val}");
                }

                if (fields.Count > 0)
                    sb.Append($" ({string.Join(", ", fields)})");
            }

            return sb.ToString();
        }

        private static string GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F2");
                case SerializedPropertyType.String:
                    return string.IsNullOrEmpty(prop.stringValue) ? null : $"\"{prop.stringValue}\"";
                // A mixed value of a [Flags] enum has no index.
                case SerializedPropertyType.Enum:
                    return prop.enumValueIndex >= 0 ? prop.enumDisplayNames[prop.enumValueIndex] : prop.intValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "None";
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return $"({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})";
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return $"({v2.x:F1},{v2.y:F1})";
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return $"({v3.x:F1},{v3.y:F1},{v3.z:F1})";
                default:
                    return null;
            }
        }
    }
}
#endif