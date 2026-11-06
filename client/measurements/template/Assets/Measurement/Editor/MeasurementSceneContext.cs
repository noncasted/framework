using UnityEngine;

namespace Measurement
{
    public sealed class MeasurementSceneContext
    {
        public MeasurementSceneContext(MeasurementRun run, Camera camera, MeasurementAssets assets, string folder)
        {
            Run = run;
            Camera = camera;
            Assets = assets;
            Folder = folder;
        }

        public MeasurementRun Run { get; }
        public MeasurementAssets Assets { get; }
        public Camera Camera { get; }

        // The folder of the generated assets of the project (Assets/Generated).
        public string Folder { get; }
    }
}
