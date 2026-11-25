using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
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
            var sources = new List<PrefabSource>
            {
                new("Assets/Menu/Shop.prefab", Group, "Shop", null, ""),
                new("Assets/GamePlay/Shop.prefab", Group, "Shop", null, "")
            };

            Assert.Throws<InvalidOperationException>(() => PrefabGroupBuilder.BuildGroups(sources));
        }

        // The root type of a prefab is the type of its catalogue property: losing it silently changes the API.
        [Test]
        public void PrefabWithoutItsRootComponent_Throws()
        {
            var prefab = new GameObject("Hero");
            _created.Add(prefab);

            var metadata = new PrefabCatalogMetadata(true, Group);
            metadata.SetRoot("Game.HeroView, Game", string.Empty);

            Assert.Throws<InvalidOperationException>(() =>
                PrefabRootResolver.ResolveComponentType("Assets/Hero.prefab", prefab, metadata));
        }

        [Test]
        public void AssetsWithTheSameName_Throw()
        {
            var sources = new List<AssetSource>
            {
                new("Assets/A/Config.asset", Group, "Config", MakeAsset()),
                new("Assets/B/Config.asset", Group, "Config", MakeAsset())
            };

            Assert.Throws<InvalidOperationException>(() => AssetGroupBuilder.BuildGroups(sources));
        }

        [Test]
        public void SoundsWithTheSameName_Throw()
        {
            var sources = new List<ClipSource>
            {
                new("Assets/A/Click.wav", Group, "Click", null, null, null, 0f),
                new("Assets/B/Click.wav", Group, "Click", null, null, null, 0f)
            };

            Assert.Throws<InvalidOperationException>(() => AudioGroupBuilder.BuildGroups(sources));
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
            var group = new AssetGroupDefinition(Group, Group + "Assets", false);
            group.AddProperty(new AssetPropertyDefinition("Config", "_config", null, "Config", "Assembly-CSharp"));

            Assert.Throws<InvalidOperationException>(() =>
                AssetGroupBuilder.AssignGeneratedTargets(new List<AssetGroupDefinition> { group }));
        }

        private EnvAsset MakeAsset()
        {
            var asset = ScriptableObject.CreateInstance<CatalogTestAsset>();
            _created.Add(asset);
            return asset;
        }
    }
}
