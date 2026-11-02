using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Internal.Tests
{
    // A failed decode leaves no clip behind: on WebGL a decoded clip is its whole PCM. The owner of the group releases
    // it once Decode has failed, so nothing may arrive after that.
    public class AudioGroupDecodeTests
    {
        private const int GoodFrames = 10000;
        private const int Attempts = 5;

        [UnityTest]
        public IEnumerator DecodeFails_Released_NoClipLeft() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;

            try
            {
                await AssertBytesOfNoMp3FailTheDecode();

                var isShown = false;

                for (var attempt = 0; attempt < Attempts && isShown == false; attempt++)
                    isShown = await DecodeFailsAndIsReleased();

                Assert.IsTrue(isShown, "No decode was on its way when Decode had failed");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        });

        private static async UniTask AssertBytesOfNoMp3FailTheDecode()
        {
            var broken = CreateAsset(new byte[] { 1, 2, 3, 4 });

            try
            {
                await EncodedAudio.Decode(broken, $"AuditBytes{Guid.NewGuid():N}");
                Assert.Fail("Decode of broken bytes succeeded");
            }
            catch (Exception exception) when (exception is not AssertionException)
            {
            }
            finally
            {
                Object.Destroy(broken);
            }
        }

        // False when the file of the long decode was read before the first await of its decode (the first decode of a
        // domain, a loaded machine): it ended before Decode returned, nothing could arrive late to be seen.
        private static async UniTask<bool> DecodeFailsAndIsReleased()
        {
            // The broken one fails before its first await, whatever the load of the machine: its temporary file goes
            // to a folder that does not exist. The names are unique: the clips are looked for by name and the
            // temporary file of a decode is named after its clip.
            var brokenName = $"AuditBroken/{Guid.NewGuid():N}";
            var goodName = $"AuditGood{Guid.NewGuid():N}";

            var group = ScriptableObject.CreateInstance<AudioGroupAsset>();
            var broken = CreateAsset(new byte[] { 1, 2, 3, 4 });
            var good = CreateAsset(SilentMp3(GoodFrames));

            try
            {
                typeof(AudioGroupAsset)
                    .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(group, new[]
                    {
                        new AudioAssetEntry(brokenName, null, broken, 1f),
                        new AudioAssetEntry(goodName, null, good, 1f)
                    });

                var decode = group.Decode();
                var isDecoding = IsDecoding(goodName);

                try
                {
                    await decode;
                    Assert.Fail("Decode with a failed entry succeeded");
                }
                catch (Exception exception) when (exception is not AssertionException)
                {
                }

                group.ReleaseDecoded();

                // Over at once unless a decode has outlived the failure of Decode. The next frame is for Destroy.
                await UniTask.WaitUntil(() => IsDecoding(goodName) == false).Timeout(TimeSpan.FromSeconds(30));
                await UniTask.NextFrame();

                CollectionAssert.IsEmpty(FindClips(brokenName, goodName), "decoded clips nobody will destroy");

                return isDecoding;
            }
            finally
            {
                Object.Destroy(group);
                Object.Destroy(broken);
                Object.Destroy(good);
            }
        }

        private static string[] FindClips(params string[] names)
        {
            return Resources.FindObjectsOfTypeAll<AudioClip>()
                            .Where(clip => names.Contains(clip.name))
                            .Select(clip => clip.name)
                            .ToArray();
        }

        private static bool IsDecoding(string name)
        {
            var folder = Path.Combine(Application.temporaryCachePath, "EncodedAudio");
            return Directory.GetFiles(folder, $"{name}_*").Length > 0;
        }

        private static TextAsset CreateAsset(byte[] bytes)
        {
            return new TextAsset(new ReadOnlySpan<byte>(bytes));
        }

        // Frames of MPEG-1 Layer III, 128 kbps, 44.1 kHz, of silence.
        private static byte[] SilentMp3(int frames)
        {
            const int frameSize = 417;
            var bytes = new byte[frames * frameSize];

            for (var i = 0; i < frames; i++)
            {
                bytes[i * frameSize] = 0xFF;
                bytes[i * frameSize + 1] = 0xFB;
                bytes[i * frameSize + 2] = 0x90;
                bytes[i * frameSize + 3] = 0x00;
            }

            return bytes;
        }
    }
}
