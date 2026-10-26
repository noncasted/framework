using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    // Catalogue members are named after assets; a name that does not fit C# must stop the generation with an error
    // naming it, never produce code that breaks the content assembly.
    public class CatalogNamingTests
    {
        [TestCase("default")]
        [TestCase("Group")]
        [TestCase("IsLoaded")]
        [TestCase("Loader")]
        [TestCase("Address")]
        [TestCase("UIPrefabs")]
        public void PrefabNamedAsMember_CompilesOrIsRefused(string name)
        {
            AssertCompilesOrRefused(() => new[] { PrefabsCatalogClassGenerator.BuildGroupClass(PrefabGroup(name)) },
                name);
        }

        [Test]
        public void PrefabsDifferingInCase_CompileOrAreRefused()
        {
            AssertCompilesOrRefused(
                () => new[] { PrefabsCatalogClassGenerator.BuildGroupClass(PrefabGroup("Hero", "hero")) },
                "hero");
        }

        [TestCase("default")]
        [TestCase("Group")]
        [TestCase("Loader")]
        [TestCase("UIAssets")]
        public void AssetNamedAsMember_CompilesOrIsRefused(string name)
        {
            var group = new AssetGroupDefinition
            {
                Name = "UI",
                ClassName = "UIAssets",
                TargetNamespace = "Internal",
                Address = "address",
                Properties =
                {
                    new AssetPropertyDefinition
                    {
                        PropertyName = name,
                        FieldName = ToFieldName(name),
                        TypeFullName = "Internal.EnvAsset"
                    }
                }
            };

            AssertCompilesOrRefused(() => new[] { AssetsCatalogClassGenerator.BuildGroupClass(group) }, name);
        }

        [TestCase("default")]
        [TestCase("IsLoaded")]
        [TestCase("Loader")]
        [TestCase("UIAudio")]
        public void SoundNamedAsMember_CompilesOrIsRefused(string name)
        {
            var group = new AudioGroupDefinition
            {
                Name = "UI",
                ClassName = "UIAudio",
                Address = "address",
                Properties = { new AudioPropertyDefinition { PropertyName = name, FieldName = ToFieldName(name) } }
            };

            AssertCompilesOrRefused(() => new[] { AudioCatalogClassGenerator.BuildGroupClass(group) }, name);
        }

        // The group Sprites is a member of the class Sprites.
        [Test]
        public void SpriteGroupNamedSprites_CompilesOrIsRefused()
        {
            AssertCompilesOrRefused(() => SpritesClassGenerator.BuildSources(new[] { SpriteGroup("Sprites", "Icon") })
                .Values.ToArray(), "Sprites");
        }

        [TestCase("Address")]
        [TestCase("EnsureLoaded")]
        [TestCase("UISprites")]
        public void SpriteNamedAsMember_CompilesOrIsRefused(string name)
        {
            AssertCompilesOrRefused(() => SpritesClassGenerator.BuildSources(new[] { SpriteGroup("UI", name) })
                .Values.ToArray(), name);
        }

        [Test]
        public void ColorGroupNamedColors_CompilesOrIsRefused()
        {
            AssertCompilesOrRefused(() => new[] { ColorSource(("Colors", "Red")) }, "Colors");
        }

        [Test]
        public void ColorNamedAsItsGroupClass_CompilesOrIsRefused()
        {
            AssertCompilesOrRefused(() => new[] { ColorSource(("Main", "MainColors")) }, "MainColors");
        }

        private static void AssertCompilesOrRefused(Func<string[]> build, string name)
        {
            string[] sources;

            try
            {
                sources = build();
            }
            catch (InvalidOperationException exception)
            {
                Assert.That(exception.Message, Does.Contain($"'{name}'"));
                return;
            }

            Assert.That(GeneratedCodeCompiler.Compile(sources), Is.Empty, "Compile errors");
        }

        private static PrefabGroupDefinition PrefabGroup(params string[] names)
        {
            var group = new PrefabGroupDefinition
            {
                Name = "UI",
                ClassName = "UIPrefabs",
                Address = "address"
            };

            foreach (var name in names)
            {
                group.Properties.Add(new PrefabPropertyDefinition
                {
                    PropertyName = name,
                    FieldName = ToFieldName(name),
                    IsGameObject = true,
                    CodeTypeName = "global::UnityEngine.GameObject"
                });
            }

            return group;
        }

        private static SpriteGroupDefinition SpriteGroup(string path, string spriteName)
        {
            return new SpriteGroupDefinition
            {
                Name = path,
                Path = path,
                ClassName = path + "Sprites",
                Address = "address",
                Properties =
                {
                    new SpritePropertyDefinition
                    {
                        PropertyName = spriteName,
                        FieldName = ToFieldName(spriteName),
                        Kind = SpriteKind.Sheet
                    }
                }
            };
        }

        private static string ColorSource(params (string group, string color)[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<ColorCatalog>();

            try
            {
                var groups = (List<ColorGroup>)catalog.Groups;

                foreach (var (group, color) in entries)
                {
                    groups.Add(new ColorGroup
                    {
                        Name = group,
                        Entries = { new ColorEntry { Name = color, Value = Color.red } }
                    });
                }

                return ColorGenerator.BuildSource(catalog, out _);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        // The generators derive the backing field the same way.
        private static string ToFieldName(string propertyName)
        {
            return "_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
        }
    }
}
