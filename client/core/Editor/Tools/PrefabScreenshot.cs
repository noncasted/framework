#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
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
                    canvasGo = PrefabScreenshotFraming.SetupUICapture(instance, cam, resolution);
                }
                else
                {
                    var bounds = PrefabScreenshotFraming.Calculate3DBounds(instance);
                    lightGo = new GameObject("_ScreenshotLight");
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1f;
                    lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                    var isFlat = bounds.size.z < 0.01f;
                    if (isFlat)
                    {
                        PrefabScreenshotFraming.SetupOrthographicCamera(cam, bounds);
                    }
                    else
                    {
                        PrefabScreenshotFraming.SetupPerspectiveCamera(cam, bounds);
                    }
                }

                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                tex.Apply();

                var bytes = tex.EncodeToPNG();
                File.WriteAllBytes(outputPath, bytes);

                var hierarchy = PrefabHierarchyDump.Build(instance, 0);

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

        private static bool HasComponent<T>(GameObject obj) where T : Component
        {
            return obj.GetComponentInChildren<T>(true) != null;
        }
    }
}
#endif
