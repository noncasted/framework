using System.Collections;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Settings.Tests
{
    // The saves are loaded by AddSaves while the scope is built and the settings read them when they are built: every
    // service that takes the settings sees the saved values in its OnSetup, whatever its place among the registrations.
    public class SettingsContainerTests
    {
        [UnityTest]
        public IEnumerator Setup_ListenerBeforeTheSettings_SeesTheSavedVolume() => UniTask.ToCoroutine(async () =>
        {
            SettingsContainerProbe.Reset(0.25f);
            GeneratedRoots.Register(((System.Func<IScopeBuilder, UniTask>)SettingsContainerRoots.ListenerFirst).Method);

            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, SettingsContainerRoots.ListenerFirst));

            try
            {
                Assert.AreEqual(0.25f, SettingsContainerProbe.SeenVolume);
            }
            finally
            {
                await scope.Dispose();
                Object.DestroyImmediate(SettingsContainerProbe.Options);
            }
        });
    }
}
