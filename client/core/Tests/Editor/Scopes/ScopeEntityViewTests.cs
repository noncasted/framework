using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.Tests
{
    [Category("Scopes")]
    public class ScopeEntityViewTests
    {
        // A stale list of components (a component replaced by hand) must say what is wrong and where.
        [Test]
        public void CreateViews_ComponentIsNotEntityComponent_ThrowsWithItsType()
        {
            var gameObject = new GameObject("ScopeEntityViewTest");

            try
            {
                var view = gameObject.AddComponent<ScopeEntityView>();
                var child = new GameObject("Child");
                child.transform.SetParent(gameObject.transform);
                // A runtime MonoBehaviour that is not an IEntityComponent: editor scripts cannot be added.
                var stranger = child.AddComponent<ScopeEntityView>();
                var field = typeof(ScopeEntityView).GetField("_autoDetected", BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(view, new List<MonoBehaviour> { stranger });

                var exception = Assert.Throws<InvalidOperationException>(() => view.CreateViews(null));

                StringAssert.Contains(typeof(ScopeEntityView).FullName, exception.Message);
                StringAssert.Contains("ScopeEntityViewTest", exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
