using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Internal.EditorTests
{
    // The bindings class mirrors object names; every name the hierarchy can give must still compile.
    public class HierarchyBindingsNamingTests
    {
        private const string Namespace = "Generated.Tests";

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _created)
            {
                if (target != null)
                    Object.DestroyImmediate(target);
            }

            _created.Clear();
        }

        // The check itself: an ordinary hierarchy compiles, broken code does not.
        [Test]
        public void PlainHierarchy_Compiles_BrokenCodeDoesNot()
        {
            var root = Make("Card");
            Make("Title", root);
            Make("Icon", root);

            AssertCompiles(root, "CardBindings");
            Assert.That(GeneratedCodeCompiler.Compile("class A { int B; int B; }"), Has.Some.Contains("CS0102"));
        }

        [Test]
        public void ChildNamedAsRoot_Compiles()
        {
            var root = Make("Popup");
            Make("Popup", root);

            AssertCompiles(root, "PopupBindings");
        }

        [Test]
        public void GrandchildNamedAsChild_Compiles()
        {
            var root = Make("Panel");
            var icon = Make("Icon", root);
            Make("Icon", icon);

            AssertCompiles(root, "PanelBindings");
        }

        [Test]
        public void EntityChildNamedRegister_CompilesOrIsRefused()
        {
            var root = Make("Hero");
            Make("Register", root);

            AssertCompilesOrRefused(root, "HeroBindings", "Register", false, true);
        }

        [Test]
        public void SceneServiceChildNamedCreate_CompilesOrIsRefused()
        {
            var root = Make("Hud");
            Make("Create", root);

            AssertCompilesOrRefused(root, "HudBindings", "Create", true, false);
        }

        [TestCase("IsSceneService")]
        [TestCase("IsEntityComponent")]
        [TestCase("LayerAssetGuid")]
        public void ChildNamedAsObjectBindingsMember_CompilesOrIsRefused(string name)
        {
            var root = Make("Menu");
            Make(name, root);

            AssertCompilesOrRefused(root, "MenuBindings", name, false, false);
        }

        // A nested class is a plain [Serializable] class: MonoBehaviour members are not its members.
        [TestCase("Destroy")]
        [TestCase("Invoke")]
        public void NestedChildNamedAsMonoBehaviourMember_Compiles(string name)
        {
            var root = Make("Window");
            var panel = Make("Panel", root);
            Make(name, panel);

            AssertCompiles(root, "WindowBindings");
        }

        [Test]
        public void NamesWithTheSameFieldName_Compile()
        {
            var root = Make("Status");
            Make("HPBar", root);
            Make("HpBar", root);

            AssertCompiles(root, "StatusBindings");
        }

        private GameObject Make(string name, GameObject parent = null)
        {
            var target = new GameObject(name);

            if (parent != null)
                target.transform.SetParent(parent.transform, false);
            else
                _created.Add(target);

            return target;
        }

        private static void AssertCompiles(GameObject root, string typeName)
        {
            var code = Generate(root, typeName, false, false, out var errors);

            Assert.That(errors, Is.Empty, "Generation errors");
            Assert.That(GeneratedCodeCompiler.Compile(code), Is.Empty, "Compile errors");
        }

        // A collision may be solved either way: the generated code compiles, or the generation is refused with an
        // error that names the object, but never code that does not compile.
        private static void AssertCompilesOrRefused(
            GameObject root,
            string typeName,
            string name,
            bool isSceneService,
            bool isEntityComponent)
        {
            var code = Generate(root, typeName, isSceneService, isEntityComponent, out var errors);

            if (errors.Count > 0)
            {
                Assert.That(string.Join("\n", errors), Does.Contain(name));
                return;
            }

            Assert.That(GeneratedCodeCompiler.Compile(code), Is.Empty, "Compile errors");
        }

        private static string Generate(
            GameObject root,
            string typeName,
            bool isSceneService,
            bool isEntityComponent,
            out List<string> errors)
        {
            errors = new List<string>();
            var layer = HierarchyBindingsLayer.Resolve(string.Empty, false);
            var node = HierarchyBindingsScanner.Scan(root, typeName, layer, errors, isSceneService, isEntityComponent);

            if (node == null)
                return null;

            return HierarchyBindingsCodeGenerator.Build(
                    node,
                    Namespace,
                    HierarchyBindingsStructure.ComputeHash(node),
                    isSceneService,
                    isEntityComponent
                );
        }
    }
}
