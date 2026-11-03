using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Global.Inputs;
using Internal;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Global.UI.Tests
{
    // The notice covers everything for good: with a gamepad the selection stays on its button in the frames that follow,
    // whichever window was opened before or after it.
    public class ClientOutdatedNoticeTests
    {
        // The built-in UI layer: in every project.
        private const string CanvasLayer = "UI";
        private const float PlaneDistance = 1f;

        private readonly System.Collections.Generic.List<GameObject> _created = new();

        private Camera _camera;

        [UnityTest]
        public IEnumerator Gamepad_SelectionStaysInTheNotice()
        {
            LogAssert.ignoreFailingMessages = true;

            var lifetime = new Internal.Lifetime();

            var events = Create("events");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
            yield return null;

            var options = ScriptableObject.CreateInstance<InputOptions>();
            var navigation = new UINavigation(new NoUpdater(), new GamepadScheme(), new NoGamepadReader(), options);
            navigation.OnSetup(lifetime);

            navigation.Add(lifetime, CreateWindow("older"));
            navigation.OnUpdate(0f);
            Assert.AreEqual("older", SelectedRoot());

            var notice = CreateNotice(lifetime, navigation);
            notice.Show();

            navigation.OnUpdate(0f);
            Assert.AreEqual("ClientOutdatedNotice", SelectedRoot(), "the gamepad selects a button under the shade");

            navigation.Add(lifetime, CreateWindow("later"));
            navigation.OnUpdate(0f);
            Assert.AreEqual("ClientOutdatedNotice", SelectedRoot(), "a window opened later takes the gamepad away");

            lifetime.Terminate();
            UnityEngine.Object.Destroy(options);
            LogAssert.ignoreFailingMessages = false;
        }

        // A Screen Space - Overlay canvas loses its projection on iPadOS Safari: the notice goes to the UI camera of the game.
        [UnityTest]
        public IEnumerator Notice_IsDrawnByTheScreenCanvasCamera()
        {
            LogAssert.ignoreFailingMessages = true;

            var lifetime = new Internal.Lifetime();

            var events = Create("events");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
            yield return null;

            var options = ScriptableObject.CreateInstance<InputOptions>();
            var navigation = new UINavigation(new NoUpdater(), new GamepadScheme(), new NoGamepadReader(), options);
            navigation.OnSetup(lifetime);

            var notice = CreateNotice(lifetime, navigation);
            notice.Show();

            var canvas = GameObject.Find("ClientOutdatedNotice").GetComponent<Canvas>();
            var layer = LayerMask.NameToLayer(CanvasLayer);

            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.AreSame(_camera, canvas.worldCamera);
            Assert.AreEqual(PlaneDistance, canvas.planeDistance);

            foreach (var transform in canvas.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(layer, transform.gameObject.layer, transform.name);

            lifetime.Terminate();
            UnityEngine.Object.Destroy(options);
            LogAssert.ignoreFailingMessages = false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
                UnityEngine.Object.Destroy(created);

            _created.Clear();
        }

        private ClientOutdatedNotice CreateNotice(IReadOnlyLifetime lifetime, IUINavigation navigation)
        {
            var style = new ClientOutdatedNoticeStyle(TMP_Settings.defaultFontAsset);

            _camera = Create("camera").AddComponent<Camera>();
            _camera.enabled = false;
            _camera.cullingMask = 1 << LayerMask.NameToLayer(CanvasLayer);
            var camera = _camera;
            var screenCanvas = new ScreenCanvasCamera(() => camera, CanvasLayer, PlaneDistance);

            return new ClientOutdatedNotice(lifetime, new NoLoadingScreen(), navigation, screenCanvas, style);
        }

        private static string SelectedRoot()
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            return selected == null ? "<nothing>" : selected.transform.root.name;
        }

        private RectTransform CreateWindow(string name)
        {
            var root = Create(name);
            root.AddComponent<RectTransform>();
            root.AddComponent<Canvas>();

            var buttonObject = new GameObject("button", typeof(RectTransform));
            buttonObject.transform.SetParent(root.transform, false);
            var image = buttonObject.AddComponent<Image>();
            buttonObject.AddComponent<Button>().targetGraphic = image;

            return (RectTransform)root.transform;
        }

        private GameObject Create(string name)
        {
            var created = new GameObject(name);
            _created.Add(created);
            return created;
        }

        private class GamepadScheme : IInputScheme
        {
            public IViewableProperty<InputSchemeType> Current { get; } = new ViewableProperty<InputSchemeType>(InputSchemeType.Gamepad);
        }

        private class NoLoadingScreen : ILoadingScreen
        {
            public UniTask Show() => UniTask.CompletedTask;

            public void ShowInstantly()
            {
            }

            public void Hide()
            {
            }

            public UniTask WaitHidden(IReadOnlyLifetime lifetime) => UniTask.CompletedTask;
        }
    }
}
