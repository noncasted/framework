using Internal;
using UnityEngine;

namespace Global.Cameras
{
    public static class CameraExtensions
    {
        // The camera of the global scope: shown while no scene has its own (loading, between the scenes).
        public static IScopeBuilder AddCamera(this IScopeBuilder builder, GlobalCamera prefab)
        {
            builder.Register<CurrentCamera>()
                   .As<ICurrentCamera>();

            var camera = builder.Instantiate(prefab, new Vector3(0f, 0f, -10f));
            camera.gameObject.SetActive(false);

            builder.RegisterComponent(camera)
                   .As<IGlobalCamera>();

            builder.Register<CameraUtils>()
                   .As<ICameraUtils>();

            return builder;
        }
    }
}
