using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    // The catalogue assets of the games hold the keys the records had as public fields (Name, Clip, Volume...). This
    // reads real .asset files with those keys through the importer: a key that stops being read turns the data of a
    // game into defaults without an error. Each test also saves the asset and reads it again.
    public class CatalogOldYamlTests
    {
        private const string FolderName = "CatalogOldYamlTests_Temp";
        private const string Folder = "Assets/" + FolderName;

        // Every test writes files of its own names and reads only them, so they stay until the end of the class.
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            AssetDatabase.CreateFolder("Assets", FolderName);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void EnvAssetGroup_OldKeys_ReadsEveryField()
        {
            var target = CreateEnvAsset();
            var path = WriteAsset<EnvAssetGroupAsset>("Assets", "Internal::Internal.EnvAssetGroupAsset",
                "  _entries:\n"
                + "  - Name: First\n"
                + $"    Asset: {Reference(target)}\n"
                + "  - Name: Second\n"
                + "    Asset: {fileID: 0}\n");

            AssertRead(path, (EnvAssetGroupAsset asset) =>
            {
                var entries = Entries<EnvAssetEntry>(asset);

                Assert.AreEqual(2, entries.Length);
                Assert.AreEqual("First", entries[0].Name);
                Assert.AreEqual(target, entries[0].Asset);
                Assert.AreEqual("Second", entries[1].Name);
                Assert.IsTrue(entries[1].Asset == null);
                Assert.AreEqual(target, asset.Get<OptionsContainer>("First"));
            });
        }

        [Test]
        public void AudioGroup_OldKeys_ReadsEveryField()
        {
            var clip = CreateClip();
            var encoded = CreateText();
            var path = WriteAsset<AudioGroupAsset>("Audio", "Internal::Internal.AudioGroupAsset",
                "  _entries:\n"
                + "  - Name: Played\n"
                + $"    Clip: {Reference(clip)}\n"
                + "    Encoded: {fileID: 0}\n"
                + "    Volume: 0.25\n"
                + "  - Name: Packed\n"
                + "    Clip: {fileID: 0}\n"
                + $"    Encoded: {Reference(encoded)}\n"
                + "    Volume: 0.75\n");

            AssertRead(path, (AudioGroupAsset asset) =>
            {
                var entries = Entries<AudioAssetEntry>(asset);

                Assert.AreEqual(2, entries.Length);
                Assert.AreEqual("Played", entries[0].Name);
                Assert.AreEqual(clip, entries[0].Clip);
                Assert.IsTrue(entries[0].Encoded == null);
                Assert.AreEqual(0.25f, entries[0].Volume);
                Assert.AreEqual("Packed", entries[1].Name);
                Assert.IsTrue(entries[1].Clip == null);
                Assert.AreEqual(encoded, entries[1].Encoded);
                Assert.AreEqual(0.75f, entries[1].Volume);
                Assert.AreEqual(0.25f, asset.GetVolume("Played", 1f));
            });
        }

        [Test]
        public void ColorCatalog_OldKeys_ReadsEveryField()
        {
            var path = WriteAsset<ColorCatalog>("Colors", "Internal::Internal.ColorCatalog",
                "  _groups:\n"
                + "  - Name: game\n"
                + "    Entries:\n"
                + "    - Name: text_active\n"
                + "      Value: {r: 0.25, g: 0.5, b: 0.75, a: 0.125}\n"
                + "    - Name: text_inactive\n"
                + "      Value: {r: 0.5, g: 0.25, b: 0.125, a: 1}\n"
                + "  - Name: mines_around\n"
                + "    Entries:\n"
                + "    - Name: 1\n"
                + "      Value: {r: 0.125, g: 0.25, b: 0.5, a: 1}\n");

            AssertRead(path, (ColorCatalog asset) =>
            {
                Assert.AreEqual(2, asset.Groups.Count);

                var game = asset.Groups[0];
                Assert.AreEqual("game", game.Name);
                Assert.AreEqual(2, game.Entries.Count);
                Assert.AreEqual("text_active", game.Entries[0].Name);
                Assert.AreEqual(new Color(0.25f, 0.5f, 0.75f, 0.125f), game.Entries[0].Value);
                Assert.AreEqual("text_inactive", game.Entries[1].Name);
                Assert.AreEqual(new Color(0.5f, 0.25f, 0.125f, 1f), game.Entries[1].Value);

                var mines = asset.Groups[1];
                Assert.AreEqual("mines_around", mines.Name);
                Assert.AreEqual(1, mines.Entries.Count);
                Assert.AreEqual("1", mines.Entries[0].Name);
                Assert.AreEqual(new Color(0.125f, 0.25f, 0.5f, 1f), mines.Entries[0].Value);
            });
        }

        [Test]
        public void PrefabGroup_OldKeys_ReadsEveryField()
        {
            var prefab = CreatePrefab();
            const string componentType = "UnityEngine.Transform, UnityEngine.CoreModule";
            var path = WriteAsset<PrefabGroupAsset>("Prefabs", "Internal::Internal.PrefabGroupAsset",
                "  _entries:\n"
                + "  - Name: Card\n"
                + $"    Prefab: {Reference(prefab)}\n"
                + $"    ComponentType: {componentType}\n");

            AssertRead(path, (PrefabGroupAsset asset) =>
            {
                var entries = Entries<PrefabAssetEntry>(asset);

                Assert.AreEqual(1, entries.Length);
                Assert.AreEqual("Card", entries[0].Name);
                Assert.AreEqual(prefab, entries[0].Prefab);
                Assert.AreEqual(componentType, entries[0].ComponentType);
                Assert.AreEqual(prefab, asset.GetGameObject("Card"));
            });
        }

        [Test]
        public void SpriteGroup_OldKeys_ReadsEveryField()
        {
            var sprite = CreateSprite();
            var path = WriteAsset<SpriteGroupAsset>("Sprites", "Internal::Internal.SpriteGroupAsset",
                "  _entries:\n"
                + "  - Name: Card\n"
                + "    Kind: 0\n"
                + "    Sprites:\n"
                + $"    - {Reference(sprite)}\n"
                + "    Time: 0.8\n"
                + "    Color: {r: 1, g: 1, b: 1, a: 1}\n"
                + "  - Name: Explosion\n"
                + "    Kind: 1\n"
                + "    Sprites:\n"
                + $"    - {Reference(sprite)}\n"
                + $"    - {Reference(sprite)}\n"
                + "    Time: 0.375\n"
                + "    Color: {r: 0.25, g: 0.5, b: 0.75, a: 0.125}\n");

            AssertRead(path, (SpriteGroupAsset asset) =>
            {
                var entries = Entries<SpriteEntry>(asset);

                Assert.AreEqual(2, entries.Length);
                Assert.AreEqual("Card", entries[0].Name);
                Assert.AreEqual(SpriteKind.Sheet, entries[0].Kind);
                Assert.AreEqual(1, entries[0].Sprites.Length);
                Assert.AreEqual(sprite, entries[0].Sprites[0]);
                Assert.AreEqual(0.8f, entries[0].Time);
                Assert.AreEqual(Color.white, entries[0].Color);
                Assert.AreEqual("Explosion", entries[1].Name);
                Assert.AreEqual(SpriteKind.Animation, entries[1].Kind);
                Assert.AreEqual(2, entries[1].Sprites.Length);
                Assert.AreEqual(sprite, entries[1].Sprites[1]);
                Assert.AreEqual(0.375f, entries[1].Time);
                Assert.AreEqual(new Color(0.25f, 0.5f, 0.75f, 0.125f), entries[1].Color);
                Assert.AreEqual(sprite, asset.GetSheet("Card"));
                Assert.IsTrue(asset.HasAnimation("Explosion"));
            });
        }

        private static void AssertRead<T>(string path, Action<T> assert) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsTrue(asset != null, $"{path} is not read as {typeof(T).Name}");
            assert(asset);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            assert(AssetDatabase.LoadAssetAtPath<T>(path));
        }

        private static TEntry[] Entries<TEntry>(ScriptableObject asset)
        {
            var field = asset.GetType().GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            return (TEntry[])field.GetValue(asset);
        }

        // The header is the one of a catalogue asset of a game; only the script and the body differ.
        private static string WriteAsset<T>(string name, string classIdentifier, string body)
            where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            var script = MonoScript.FromScriptableObject(instance);
            Object.DestroyImmediate(instance);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out var guid, out long _);
            Assert.IsNotEmpty(guid);

            var path = $"{Folder}/{name}.asset";
            var text = "%YAML 1.1\n"
                + "%TAG !u! tag:unity3d.com,2011:\n"
                + "--- !u!114 &11400000\n"
                + "MonoBehaviour:\n"
                + "  m_ObjectHideFlags: 0\n"
                + "  m_CorrespondingSourceObject: {fileID: 0}\n"
                + "  m_PrefabInstance: {fileID: 0}\n"
                + "  m_PrefabAsset: {fileID: 0}\n"
                + "  m_GameObject: {fileID: 0}\n"
                + "  m_Enabled: 1\n"
                + "  m_EditorHideFlags: 0\n"
                + $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n"
                + $"  m_Name: {name}\n"
                + $"  m_EditorClassIdentifier: {classIdentifier}\n"
                + body;

            File.WriteAllText(path, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        private static string Reference(Object target)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out var guid, out long id);
            Assert.IsNotEmpty(guid);

            // Type 2 is an asset Unity stores as text with its own script, type 3 a file with an importer.
            var type = target is ScriptableObject ? 2 : 3;
            return $"{{fileID: {id}, guid: {guid}, type: {type}}}";
        }

        private static OptionsContainer CreateEnvAsset()
        {
            var path = $"{Folder}/Target.asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<OptionsContainer>(), path);
            return AssetDatabase.LoadAssetAtPath<OptionsContainer>(path);
        }

        private static TextAsset CreateText()
        {
            var path = $"{Folder}/Encoded.bytes";
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }

        private static GameObject CreatePrefab()
        {
            var source = new GameObject("Prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, $"{Folder}/Prefab.prefab");
            Object.DestroyImmediate(source);
            return prefab;
        }

        private static Sprite CreateSprite()
        {
            var path = $"{Folder}/Sprite.png";
            var texture = new Texture2D(2, 2);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static AudioClip CreateClip()
        {
            var path = $"{Folder}/Clip.wav";
            File.WriteAllBytes(path, SilentWav());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // 8 kHz mono 16 bit, 100 samples of silence.
        private static byte[] SilentWav()
        {
            const int sampleRate = 8000;
            const int dataSize = 200;

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            writer.Write(new byte[dataSize]);
            writer.Flush();

            return stream.ToArray();
        }
    }
}
