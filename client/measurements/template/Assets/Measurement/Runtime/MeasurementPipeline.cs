using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace Measurement
{
    /// <summary> Clears the target and draws the unlit renderers of each camera (sprites, a camera canvas). </summary>
    public sealed class MeasurementPipeline : RenderPipeline
    {
        private static readonly ShaderTagId UnlitTag = new("SRPDefaultUnlit");

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            foreach (var camera in cameras)
                RenderCamera(context, camera);
        }

        private static void RenderCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera.TryGetCullingParameters(out var parameters) == false)
                return;

            var culling = context.Cull(ref parameters);
            context.SetupCameraProperties(camera);

            var description = new RendererListDesc(UnlitTag, culling, camera)
            {
                sortingCriteria = SortingCriteria.CommonTransparent,
                renderQueueRange = RenderQueueRange.all
            };

            var renderers = context.CreateRendererList(description);
            var commands = new CommandBuffer { name = "Measurement" };
            commands.ClearRenderTarget(true, true, camera.backgroundColor);
            commands.DrawRendererList(renderers);

            context.ExecuteCommandBuffer(commands);
            commands.Release();
            context.Submit();
        }
    }
}
