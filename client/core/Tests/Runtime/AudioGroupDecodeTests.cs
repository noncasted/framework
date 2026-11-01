using System;
using System.Collections;
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
        [UnityTest]
        public IEnumerator DecodeFails_Released_NoClipLeft() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;

            var group = ScriptableObject.CreateInstance<AudioGroupAsset>();
            var broken = CreateAsset(new byte[] { 1, 2, 3, 4 });
            // Long enough to end after the broken one has failed.
            var good = CreateAsset(SilentMp3(40000));

            try
            {
                typeof(AudioGroupAsset)
                    .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(group, new[]
                    {
                        new AudioAssetEntry { Name = "AuditBroken", Encoded = broken },
                        new AudioAssetEntry { Name = "AuditGood", Encoded = good }
                    });

                try
                {
                    await group.Decode();
                    Assert.Fail("Decode of broken bytes succeeded");
                }
                catch (Exception exception) when (exception is not AssertionException)
                {
                }

                group.ReleaseDecoded();

                await UniTask.Delay(TimeSpan.FromSeconds(3f), DelayType.Realtime);

                var left = Resources.FindObjectsOfTypeAll<AudioClip>()
                                    .Where(clip => clip.name == "AuditBroken" || clip.name == "AuditGood")
                                    .Select(clip => clip.name)
                                    .ToArray();

                CollectionAssert.IsEmpty(left, "decoded clips nobody will destroy");
            }
            finally
            {
                Object.Destroy(group);
                Object.Destroy(broken);
                Object.Destroy(good);
                LogAssert.ignoreFailingMessages = false;
            }
        });

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
