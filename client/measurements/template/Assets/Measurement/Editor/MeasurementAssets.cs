using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Measurement
{
    /// <summary>
    ///     The assets of the measured scene, made from code in every project: each Unity version serializes them
    ///     itself, so no asset of one version is upgraded by another.
    /// </summary>
    public sealed class MeasurementAssets
    {
        public const string Folder = "Assets/Generated";

        private const int SpriteSize = 32;
        private const int SampleRate = 22050;

        private MeasurementAssets(Sprite sprite, AudioClip clip, MeasurementPipelineAsset pipeline)
        {
            Sprite = sprite;
            Clip = clip;
            Pipeline = pipeline;
        }

        public Sprite Sprite { get; }
        public AudioClip Clip { get; }
        public MeasurementPipelineAsset Pipeline { get; }

        public static MeasurementAssets Create()
        {
            Directory.CreateDirectory(Folder);

            return new MeasurementAssets(CreateSprite(), CreateClip(), CreatePipeline());
        }

        private static Sprite CreateSprite()
        {
            var path = $"{Folder}/Circle.png";
            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            var center = (SpriteSize - 1) / 2f;

            for (var y = 0; y < SpriteSize; y++)
            {
                for (var x = 0; x < SpriteSize; x++)
                {
                    var inside = new Vector2(x - center, y - center).magnitude <= center;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SpriteSize;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return Load<Sprite>(path);
        }

        // A short sine tone as a 16-bit PCM wav, imported with the default audio settings of the platform.
        private static AudioClip CreateClip()
        {
            var path = $"{Folder}/Tone.wav";
            var samples = SampleRate / 2;

            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + samples * 2);
                writer.Write("WAVEfmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write("data".ToCharArray());
                writer.Write(samples * 2);

                for (var i = 0; i < samples; i++)
                    writer.Write((short)(Math.Sin(i * 440.0 * 2 * Math.PI / SampleRate) * short.MaxValue * 0.5));
            }

            AssetDatabase.ImportAsset(path);

            return Load<AudioClip>(path);
        }

        private static MeasurementPipelineAsset CreatePipeline()
        {
            var path = $"{Folder}/Pipeline.asset";

            if (File.Exists(path) == false)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MeasurementPipelineAsset>(), path);

            return Load<MeasurementPipelineAsset>(path);
        }

        public static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
                throw new InvalidOperationException($"No {typeof(T).Name} at {path}");

            return asset;
        }
    }
}
