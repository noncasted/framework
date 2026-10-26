using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Internal
{
    [DisallowMultipleComponent]
    public class ScopeEntityView : MonoBehaviour, IScopeEntityView
    {
        [SerializeField] private List<MonoBehaviour> _autoDetected;

        private IContainer _container;

        public IContainer Container => _container;

        public void CreateViews(IEntityBuilder builder)
        {
            foreach (var behaviour in _autoDetected)
            {
                // The list is filled by Scan with IEntityComponent components only: anything else (a missing script,
                // a component replaced by hand) is a broken prefab.
                if (behaviour is not IEntityComponent component)
                {
                    var entry = behaviour == null ? "null" : behaviour.GetType().FullName;

                    throw new InvalidOperationException(
                        $"[ScopeEntityView] {name}: {entry} is not an IEntityComponent, rescan the view");
                }

                component.Register(builder);
            }
        }

        public void Bind(IContainer container)
        {
            _container = container;
        }

        [Button("Scan")]
        private void OnValidate()
        {
            _autoDetected ??= new();
            _autoDetected.Clear();
            var components = GetComponentsInChildren<IEntityComponent>(true);

            foreach (var component in components)
            {
                // The list holds MonoBehaviours: an IEntityComponent of another kind cannot be serialized in it.
                if (component is not MonoBehaviour behaviour)
                {
                    throw new InvalidOperationException(
                        $"[ScopeEntityView] {name}: {component.GetType().FullName} is an IEntityComponent but not a MonoBehaviour");
                }

                _autoDetected.Add(behaviour);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        public void Dispose()
        {
            _container?.Dispose();
        }
    }
}