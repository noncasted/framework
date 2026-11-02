using UnityEngine;
using UnityEngine.UI;

namespace Internal
{
    // Puts the camera of a prefab screenshot so that the whole prefab fits: a canvas for UI, a framed camera for the rest.
    internal static class PrefabScreenshotFraming
    {
        private const float CameraPadding = 1.3f;

        public static GameObject SetupUICapture(GameObject instance, Camera cam, int resolution)
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

        public static void SetupOrthographicCamera(Camera cam, Bounds bounds)
        {
            cam.orthographic = true;
            var maxDim = Mathf.Max(bounds.size.x, bounds.size.y);
            if (maxDim < 0.001f) maxDim = 1f;
            cam.orthographicSize = maxDim * 0.5f * CameraPadding;
            cam.transform.position = bounds.center + Vector3.back * 10f;
            cam.transform.LookAt(bounds.center);
        }

        public static void SetupPerspectiveCamera(Camera cam, Bounds bounds)
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

        public static Bounds Calculate3DBounds(GameObject obj)
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
    }
}
