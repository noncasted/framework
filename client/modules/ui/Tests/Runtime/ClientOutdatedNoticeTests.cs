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
        private readonly System.Collections.Generic.List<GameObject> _created = new();

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

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
                UnityEngine.Object.Destroy(created);

            _created.Clear();
        }

        private static ClientOutdatedNotice CreateNotice(IReadOnlyLifetime lifetime, IUINavigation navigation)
        {
            var style = new ClientOutdatedNoticeStyle { Font = TMP_Settings.defaultFontAsset };
            return new ClientOutdatedNotice(lifetime, new NoLoadingScreen(), navigation, style);
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

        private class NoUpdater : IUpdater
        {
            public IViewableProperty<float> Speed { get; } = new ViewableProperty<float>(1f);

            public void SetSpeed(float speed)
            {
            }

            public void Pause()
            {
            }

            public void Continue()
            {
            }

            public void Add(IReadOnlyLifetime l, IUpdatable u)
            {
            }

            public void Add(IReadOnlyLifetime l, IPreUpdatable updatable)
            {
            }

            public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u)
            {
            }

            public void Add(IReadOnlyLifetime l, IFixedUpdatable u)
            {
            }

            public void Add(IReadOnlyLifetime l, IPostFixedUpdatable updatable)
            {
            }

            public void Add(IReadOnlyLifetime l, IGizmosUpdatable updatable)
            {
            }

            public void Add(IReadOnlyLifetime l, IPostUpdatable updatable)
            {
            }

            public void Add(IReadOnlyLifetime l, ILateUpdatable updatable)
            {
            }
        }
    }
}
