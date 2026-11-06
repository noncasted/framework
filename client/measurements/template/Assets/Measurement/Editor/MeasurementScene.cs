using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Measurement
{
    /// <summary>
    ///     Builds the measured scene: a camera, the root with the common parts (libraries, sprites, audio), then the
    ///     parts of the variant (<see cref="IMeasurementSceneStep" />: uGUI, 2D physics).
    /// </summary>
    public static class MeasurementScene
    {
        public const string Path = MeasurementAssets.Folder + "/Measurement.unity";

        public static string Build(MeasurementAssets assets)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.16f);

            var root = new GameObject("Measurement");
            var run = root.AddComponent<MeasurementRun>();

            run.AddPart(root.AddComponent<LibrariesPart>());

            var sprites = root.AddComponent<SpritesPart>();
            sprites.Init(assets.Sprite);
            run.AddPart(sprites);

            var audio = root.AddComponent<AudioPart>();
            audio.Init(assets.Clip);
            run.AddPart(audio);

            var context = new MeasurementSceneContext(run, camera, assets, MeasurementAssets.Folder);

            foreach (var type in TypeCache.GetTypesDerivedFrom<IMeasurementSceneStep>().OrderBy(type => type.FullName))
            {
                Debug.Log($"[measurement] scene step {type.FullName}");
                ((IMeasurementSceneStep)Activator.CreateInstance(type)).Build(context);
            }

            if (EditorSceneManager.SaveScene(scene, Path) == false)
                throw new InvalidOperationException($"The scene was not saved to {Path}");

            return Path;
        }
    }
}
