using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // NetworkLatencyOverlaySetup on a monitor of the test: the overlay lives while the monitor measures, takes the font of
    // its options and goes to the screen canvas camera of the game. In play mode: the overlay is created with
    // DontDestroyOnLoad and destroyed at the end of a frame.
    public class NetworkLatencyOverlaySetupTests
    {
        private const string OverlayName = nameof(NetworkLatencyOverlay);

        // The built-in UI layer: in every project.
        private const string CanvasLayer = "UI";
        private const float PlaneDistance = 1f;

        private readonly List<Harness> _harnesses = new();

        // Destroy waits for the end of the frame: the next test must not find the overlay of this one.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var harness in _harnesses)
                harness.Lifetime.Terminate();

            _harnesses.Clear();
            yield return null;
        }

        [Test]
        public void BeforeMeasuring_NoOverlay()
        {
            var harness = Create();
            harness.Setup.OnSetup(harness.Lifetime);

            Assert.IsNull(GameObject.Find(OverlayName));
        }

        [Test]
        public void MeasuringStarts_OverlayIsCreated()
        {
            var harness = Create();
            harness.Setup.OnSetup(harness.Lifetime);

            harness.Monitor.Begin();

            Assert.IsNotNull(GameObject.Find(OverlayName));
        }

        // A monitor that already measures when the scope sets up: the overlay comes at once.
        [Test]
        public void SetupWhileMeasuring_OverlayIsCreated()
        {
            var harness = Create();
            harness.Monitor.Begin();

            harness.Setup.OnSetup(harness.Lifetime);

            Assert.IsNotNull(GameObject.Find(OverlayName));
        }

        [UnityTest]
        public IEnumerator MeasuringEnds_OverlayIsDestroyed() => UniTask.ToCoroutine(async () =>
        {
            var harness = Create();
            harness.Setup.OnSetup(harness.Lifetime);
            harness.Monitor.Begin();

            harness.Monitor.End();
            await UniTask.Yield();

            Assert.IsNull(GameObject.Find(OverlayName));
        });

        [UnityTest]
        public IEnumerator LifetimeEnds_OverlayIsDestroyed() => UniTask.ToCoroutine(async () =>
        {
            var harness = Create();
            harness.Setup.OnSetup(harness.Lifetime);
            harness.Monitor.Begin();

            harness.Lifetime.Terminate();
            await UniTask.Yield();

            Assert.IsNull(GameObject.Find(OverlayName));
        });

        // A text without a font of the game took TMP_Settings.defaultFontAsset and warned in every session when there was none.
        [Test]
        public void Overlay_TakesTheFontOfTheOptions()
        {
            var harness = Create();
            harness.Setup.OnSetup(harness.Lifetime);
            harness.Monitor.Begin();

            var label = GameObject.Find(OverlayName).GetComponentInChildren<TextMeshProUGUI>(true);

            Assert.AreSame(harness.Options.Font, label.font);
        }

        [Test]
        public void OptionsWithoutAFont_Throw()
        {
            var options = ScriptableObject.CreateInstance<NetworkOverlayOptions>();

            Assert.Throws<InvalidOperationException>(() =>
                new NetworkLatencyOverlaySetup(new FakeMonitor(), options, new ScreenCanvasOverlay()));
        }

        // A Screen Space - Overlay canvas loses its projection on iPadOS Safari: the overlay goes to the UI camera of the game.
        [Test]
        public void Overlay_IsDrawnByTheScreenCanvasCamera()
        {
            var harness = Create();
            harness.Setup.OnSetup(harness.Lifetime);
            harness.Monitor.Begin();

            var canvas = GameObject.Find(OverlayName).GetComponent<Canvas>();
            var layer = LayerMask.NameToLayer(CanvasLayer);

            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.AreSame(harness.Camera, canvas.worldCamera);
            Assert.AreEqual(PlaneDistance, canvas.planeDistance);
            Assert.That(canvas.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject.layer), Is.All.EqualTo(layer));
        }

        private Harness Create()
        {
            var harness = new Harness();
            _harnesses.Add(harness);
            return harness;
        }

        private class Harness
        {
            public Harness()
            {
                Options = ScriptableObject.CreateInstance<NetworkOverlayOptions>();
                typeof(NetworkOverlayOptions).GetField("_font", BindingFlags.Instance | BindingFlags.NonPublic)
                                             .SetValue(Options, Font);
                Camera = new GameObject("NetworkLatencyOverlaySetupTests.Camera").AddComponent<Camera>();
                Camera.enabled = false;
                Camera.cullingMask = 1 << LayerMask.NameToLayer(CanvasLayer);
                var camera = Camera;
                Lifetime.Listen(() => UnityEngine.Object.Destroy(camera.gameObject));

                var screenCanvas = new ScreenCanvasCamera(() => camera, CanvasLayer, PlaneDistance);
                Setup = new NetworkLatencyOverlaySetup(Monitor, Options, screenCanvas);
            }

            public readonly Lifetime Lifetime = new();
            public readonly FakeMonitor Monitor = new();
            public readonly NetworkOverlayOptions Options;
            public readonly Camera Camera;
            public readonly NetworkLatencyOverlaySetup Setup;

            // The font the forked ugui package ships: the tests run in the editor only.
            private static TMP_FontAsset Font =>
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Packages/com.unity.ugui/Package Resources/Fonts & Materials/LiberationSans SDF.asset");
#else
                throw new InvalidOperationException("[Tests] The editor only");
#endif
        }

        private class FakeMonitor : INetworkLatencyMonitor
        {
            private readonly ViewableProperty<bool> _isMeasuring = new(false);

            public IViewableProperty<bool> IsMeasuring => _isMeasuring;
            public string Report => "Network: the report of the test";
            public float PingMs => 0f;

            public void Begin() => _isMeasuring.Set(true);
            public void End() => _isMeasuring.Set(false);

            public void OnPong(int id) => throw new InvalidOperationException("[Tests] Not a part of the overlay");

            public void OnPeerEcho(SharedSessionLatency.PeerEcho echo) =>
                throw new InvalidOperationException("[Tests] Not a part of the overlay");
        }
    }
}
