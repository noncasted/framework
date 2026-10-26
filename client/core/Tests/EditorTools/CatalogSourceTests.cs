using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    public sealed class CatalogTestAsset : EnvAsset
    {
    }

    // What the catalogues are built from: a source the generator cannot represent stops the generation loudly
    // instead of being dropped or turned into something else.
    public class CatalogSourceTests
    {
        private const string Group = "CatalogSourceTests";

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _created)
                Object.DestroyImmediate(target);

            _created.Clear();
        }

        [Test]
        public void PrefabsWithTheSameName_Throw()
        {
            var sources = new List<PrefabCatalogGenerator.PrefabSource>
            {
                new() { Path = "Assets/Menu/Shop.prefab", Group = Group, PropertyName = "Shop", ComponentType = "" },
                new() { Path = "Assets/GamePlay/Shop.prefab", Group = Group, PropertyName = "Shop", ComponentType = "" }
            };

            Assert.Throws<InvalidOperationException>(() => PrefabCatalogGenerator.BuildGroups(sources));
        }

        // The root type of a prefab is the type of its catalogue property: losing it silently changes the API.
        [Test]
        public void PrefabWithoutItsRootComponent_Throws()
        {
            var prefab = new GameObject("Hero");
            _created.Add(prefab);

            var metadata = new PrefabCatalogMetadata
            {
                Included = true,
                Group = Group,
                ComponentType = "Game.HeroView, Game"
            };

            Assert.Throws<InvalidOperationException>(() =>
                PrefabCatalogGenerator.ResolveComponentType("Assets/Hero.prefab", prefab, metadata));
        }

        [Test]
        public void AssetsWithTheSameName_Throw()
        {
            var sources = new List<AssetCatalogGenerator.AssetSource>
            {
                new() { Path = "Assets/A/Config.asset", Group = Group, PropertyName = "Config", Asset = MakeAsset() },
                new() { Path = "Assets/B/Config.asset", Group = Group, PropertyName = "Config", Asset = MakeAsset() }
            };

            Assert.Throws<InvalidOperationException>(() => AssetCatalogGenerator.BuildGroups(sources));
        }

        [Test]
        public void SoundsWithTheSameName_Throw()
        {
            var sources = new List<AudioCatalogGenerator.ClipSource>
            {
                new() { Path = "Assets/A/Click.wav", Group = Group, PropertyName = "Click" },
                new() { Path = "Assets/B/Click.wav", Group = Group, PropertyName = "Click" }
            };

            Assert.Throws<InvalidOperationException>(() => AudioCatalogGenerator.BuildGroups(sources));
        }

        // Scripts without an asmdef compile into Assembly-CSharp: no generated class can live next to them in an
        // assembly the catalogue knows, and "Assembly-CSharp" is no namespace.
        [Test]
        public void AssemblyCSharp_HasNoGeneratedFolder()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CatalogAssemblies.GetGeneratedFolder("CatalogSourceTests", "Assembly-CSharp", "Assets/Fallback"));
        }

        [Test]
        public void AssetOfAssemblyCSharp_Throws()
        {
            var group = new AssetGroupDefinition
            {
                Name = Group,
                ClassName = Group + "Assets",
                Properties =
                {
                    new AssetPropertyDefinition
                    {
                        PropertyName = "Config",
                        FieldName = "_config",
                        TypeFullName = "Config",
                        TypeAssembly = "Assembly-CSharp"
                    }
                }
            };

            Assert.Throws<InvalidOperationException>(() =>
                AssetCatalogGenerator.AssignGeneratedTargets(new List<AssetGroupDefinition> { group }));
        }

        private EnvAsset MakeAsset()
        {
            var asset = ScriptableObject.CreateInstance<CatalogTestAsset>();
            _created.Add(asset);
            return asset;
        }
    }
}
