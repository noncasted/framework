using System;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Binder of a scope without its own scene: there is nowhere to move objects, so any attempt is
    /// a scope configuration error.
    /// </summary>
    public class ExceptionServiceScopeBinder : IServiceScopeBinder
    {
        private readonly string _scopeName;

        public ExceptionServiceScopeBinder(string scopeName)
        {
            _scopeName = scopeName;
        }

        public void MoveToModules(MonoBehaviour service)
        {
            throw CreateException(service.gameObject);
        }

        public void MoveToModules(GameObject gameObject)
        {
            throw CreateException(gameObject);
        }

        public void MoveToModules(Transform transform)
        {
            throw CreateException(transform.gameObject);
        }

        private Exception CreateException(GameObject gameObject)
        {
            return new InvalidOperationException(
                $"Scope '{_scopeName}' has no service scene to move '{gameObject.name}' into. " +
                "Configure it with WithRuntimeScene or WithAssetScene.");
        }
    }
}
