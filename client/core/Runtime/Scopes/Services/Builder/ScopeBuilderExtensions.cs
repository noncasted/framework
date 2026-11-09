using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class ScopeBuilderExtensions
    {
        [ContainerPrimitive(ContainerPrimitive.SceneServices)]
        public static async UniTask FindOrLoadSceneWithServices(
            this IScopeBuilder builder,
            AssetReference scene,
            bool isMain = false)
        {
            // The scenes of a scope load as a batch through WhenAll, so the span is concurrent.
            // The scene name is unknown from the reference: it appears only after loading.
            using var scope = GameProfiler.Concurrent("Scene");

            var services = await builder.FindOrLoadScene<SceneServicesFactory>(scene, isMain);
            CreateServices(builder, scope, services);
        }

        // A scene of the content directory; the generator merges in the services of its SceneServicesFactory (Scenes.X).
        [ContainerPrimitive(ContainerPrimitive.SceneServices)]
        public static async UniTask FindOrLoadSceneWithServices(
            this IScopeBuilder builder,
            ContentScene scene,
            bool isMain = false)
        {
            using var scope = GameProfiler.Concurrent("Scene");

            var services = await builder.FindOrLoadScene<SceneServicesFactory>(scene, isMain);
            CreateServices(builder, scope, services);
        }

        private static void CreateServices(IScopeBuilder builder, IProfilerScope scope, SceneServicesFactory services)
        {
            scope.SetName($"Scene: {services.gameObject.scene.name}");
            scope.Measure("Services", () => services.Create(builder));
        }

        // A prefab is created synchronously, with all its Awake and OnEnable, right in the middle of Construct:
        // without a span of its own this time dissolves in the registrations.
        [ContainerPrimitive(ContainerPrimitive.Instantiate)]
        public static T Instantiate<T>(this IScopeBuilder builder, T prefab) where T : MonoBehaviour
        {
            using var scope = GameProfiler.Scope($"Instantiate: {prefab.name}");

            var instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
            builder.Binder.MoveToModules(instance);

            return instance;
        }

        [ContainerPrimitive(ContainerPrimitive.Instantiate)]
        public static GameObject Instantiate(this IScopeBuilder builder, GameObject prefab)
        {
            using var scope = GameProfiler.Scope($"Instantiate: {prefab.name}");

            var instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
            builder.Binder.MoveToModules(instance);

            return instance;
        }

        // An empty object of the scope, for components added from code.
        [ContainerPrimitive(ContainerPrimitive.Instantiate)]
        public static GameObject Instantiate(this IScopeBuilder builder, string name)
        {
            var instance = new GameObject(name);
            builder.Binder.MoveToModules(instance);

            return instance;
        }

        [ContainerPrimitive(ContainerPrimitive.Instantiate)]
        public static T Instantiate<T>(this IScopeBuilder builder, T prefab, Vector3 position) where T : MonoBehaviour
        {
            using var scope = GameProfiler.Scope($"Instantiate: {prefab.name}");

            var instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = prefab.name;
            builder.Binder.MoveToModules(instance);

            return instance;
        }
    }
}
