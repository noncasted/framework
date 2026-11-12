using UnityEngine;

namespace Internal
{
    public static class GameObjectLifetimeExtensions
    {
        public static IReadOnlyLifetime GetObjectLifetime(this MonoBehaviour monoBehaviour)
        {
            if (monoBehaviour.TryGetComponent(out IGameObjectLifetime lifetimeSource) == false)
                lifetimeSource = monoBehaviour.gameObject.AddComponent<GameObjectLifetime>();

            return lifetimeSource.GetValidLifetime();
        }
    }
}
