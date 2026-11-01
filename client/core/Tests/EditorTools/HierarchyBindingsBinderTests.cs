using System.Collections.Generic;
using NUnit.Framework;
using Internal.EditorTests.Menu;
using UnityEngine;

namespace Internal.EditorTests
{
    // A bind that fails leaves the object as it was: the component it added for the bind does not stay.
    public class HierarchyBindingsBinderTests
    {
        [Test]
        public void Bind_StructureChanged_ObjectUntouched()
        {
            var root = new GameObject("Shop");

            try
            {
                var errors = new List<string>();
                var request = new HierarchyBindingsRequest
                {
                    AssetPath = string.Empty,
                    ObjectNames = "Shop",
                    TypeName = "ShopBindings",
                    StructureHash = "another structure"
                };

                var isBound = HierarchyBindingsBinder.Bind(root, typeof(ShopBindings), request, errors);

                Assert.IsFalse(isBound);
                Assert.IsNotEmpty(errors);
                Assert.IsNull(root.GetComponent<ShopBindings>(), "the failed bind left its component on the object");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
