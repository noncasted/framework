using System;
using System.Collections;
using System.Collections.Generic;
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
    // The selection of the gamepad, through IUINavigation and OnUpdate: which window and which selectable get it, the
    // explicit navigation set on the selected one, the scroll to it.
    public class UINavigationTests
    {
        private readonly List<GameObject> _created = new();

        private Lifetime _lifetime;
        private InputOptions _options;
        private ViewableProperty<InputSchemeType> _scheme;
        private FakeGamepad _gamepad;
        private UINavigation _navigation;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
            _options = ScriptableObject.CreateInstance<InputOptions>();
            _scheme = new ViewableProperty<InputSchemeType>(InputSchemeType.Gamepad);
            _gamepad = new FakeGamepad();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();

            foreach (var created in _created)
                UnityEngine.Object.DestroyImmediate(created);

            _created.Clear();
            UnityEngine.Object.DestroyImmediate(_options);
        }

        [UnityTest]
        public IEnumerator Setup_NoEventSystem_Throws()
        {
            // the objects of an earlier test of the assembly are destroyed by the end of its frame
            yield return null;

            Assert.IsTrue(EventSystem.current == null);
            Assert.Throws<MissingReferenceException>(() => CreateNavigation().OnSetup(_lifetime));
        }

        [Test]
        public void Setup_NoStandaloneInputModule_Throws()
        {
            Create("events").AddComponent<EventSystem>();

            Assert.Throws<MissingComponentException>(() => CreateNavigation().OnSetup(_lifetime));
        }

        [Test]
        public void Setup_OverridesTheInputOfTheModule()
        {
            Setup();

            var input = EventSystem.current.GetComponent<GamepadUIInput>();

            Assert.IsNotNull(input);
            Assert.AreSame(input, EventSystem.current.GetComponent<StandaloneInputModule>().inputOverride);
        }

        [Test]
        public void EnableNavigation_MakesASelectableACandidate()
        {
            Setup();

            var root = CreateRoot("window");
            var button = CreateButton(root, "button", Vector2.zero);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);
            Assert.IsNull(Selected());

            button.EnableNavigation();
            Assert.AreEqual(Navigation.Mode.Automatic, button.navigation.mode);

            _navigation.OnUpdate(0f);
            Assert.AreSame(button.gameObject, Selected());
        }

        [Test]
        public void FirstSelection_FirstCandidateInTheHierarchy()
        {
            Setup();

            var root = CreateRoot("window");
            var first = CreateButton(root, "first", Vector2.zero);
            CreateButton(root, "second", new Vector2(0f, -200f));
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);

            Assert.AreSame(first.gameObject, Selected());
        }

        [Test]
        public void FirstSelection_SkipsWhatIsNotACandidate()
        {
            Setup();

            var root = CreateRoot("window");

            var inactive = CreateButton(root, "inactive", Vector2.zero);
            inactive.gameObject.SetActive(false);

            var disabled = CreateButton(root, "disabled", Vector2.zero);
            disabled.enabled = false;

            var notInteractable = CreateButton(root, "not interactable", Vector2.zero);
            notInteractable.interactable = false;

            var inGroup = CreateButton(root, "in a group", Vector2.zero);
            inGroup.gameObject.AddComponent<CanvasGroup>().interactable = false;

            var noNavigation = CreateButton(root, "no navigation", Vector2.zero);
            noNavigation.navigation = new Navigation { mode = Navigation.Mode.None };

            CreateRect(root, "field", Vector2.zero, new Vector2(100f, 50f)).gameObject.AddComponent<TMP_InputField>();

            var candidate = CreateButton(root, "candidate", Vector2.zero);
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);

            Assert.AreSame(candidate.gameObject, Selected());
        }

        [Test]
        public void FirstSelection_UnderStartBeforeTheRestOfTheRoot()
        {
            Setup();

            var root = CreateRoot("window");
            CreateButton(root, "room code", Vector2.zero);
            var start = CreateRect(root, "buttons", Vector2.zero, new Vector2(500f, 500f));
            var first = CreateButton(start, "button", Vector2.zero);
            _navigation.Add(_lifetime, root, start);

            _navigation.OnUpdate(0f);

            Assert.AreSame(first.gameObject, Selected());
        }

        [Test]
        public void FirstSelection_NothingUnderStart_FirstOfTheRoot()
        {
            Setup();

            var root = CreateRoot("window");
            var first = CreateButton(root, "room code", Vector2.zero);
            var start = CreateRect(root, "empty", Vector2.zero, new Vector2(500f, 500f));
            _navigation.Add(_lifetime, root, start);

            _navigation.OnUpdate(0f);

            Assert.AreSame(first.gameObject, Selected());
        }

        [Test]
        public void Add_StartOutOfTheRoot_Throws()
        {
            Setup();

            var root = CreateRoot("window");
            var other = CreateRoot("other");

            Assert.Throws<ArgumentException>(() => _navigation.Add(_lifetime, root, other));
        }

        [Test]
        public void NoCandidates_SelectionIsDropped()
        {
            Setup();

            var root = CreateRoot("window");
            var button = CreateButton(root, "button", Vector2.zero);
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);
            Assert.AreSame(button.gameObject, Selected());

            button.interactable = false;
            _navigation.OnUpdate(0f);

            Assert.IsNull(Selected());
            Assert.AreEqual(Navigation.Mode.Automatic, button.navigation.mode, "the own navigation is given back");
        }

        [Test]
        public void NoWindow_SelectionIsDropped()
        {
            Setup();

            var windowLifetime = new Lifetime();
            var root = CreateRoot("window");
            var button = CreateButton(root, "button", Vector2.zero);
            _navigation.Add(windowLifetime, root);

            _navigation.OnUpdate(0f);
            Assert.AreSame(button.gameObject, Selected());

            windowLifetime.Terminate();
            _navigation.OnUpdate(0f);

            Assert.IsNull(Selected());
            Assert.AreEqual(Navigation.Mode.Automatic, button.navigation.mode, "the own navigation is given back");
        }

        [Test]
        public void TopWindow_LastAddedActive()
        {
            Setup();

            var older = CreateRoot("older");
            var olderButton = CreateButton(older, "button", Vector2.zero);
            var later = CreateRoot("later");
            var laterButton = CreateButton(later, "button", Vector2.zero);
            _navigation.Add(_lifetime, older);
            _navigation.Add(_lifetime, later);

            _navigation.OnUpdate(0f);
            Assert.AreSame(laterButton.gameObject, Selected());

            later.gameObject.SetActive(false);
            _navigation.OnUpdate(0f);
            Assert.AreSame(olderButton.gameObject, Selected());

            later.gameObject.SetActive(true);
            _navigation.OnUpdate(0f);
            Assert.AreSame(laterButton.gameObject, Selected());
        }

        [Test]
        public void TopWindow_LifetimeEnded_ThePreviousOne()
        {
            Setup();

            var older = CreateRoot("older");
            var olderButton = CreateButton(older, "button", Vector2.zero);
            var later = CreateRoot("later");
            CreateButton(later, "button", Vector2.zero);
            var laterLifetime = new Lifetime();
            _navigation.Add(_lifetime, older);
            _navigation.Add(laterLifetime, later);

            _navigation.OnUpdate(0f);
            laterLifetime.Terminate();
            _navigation.OnUpdate(0f);

            Assert.AreSame(olderButton.gameObject, Selected());
        }

        [Test]
        public void TopWindow_DestroyedRoot_IsSkipped()
        {
            Setup();

            var older = CreateRoot("older");
            var olderButton = CreateButton(older, "button", Vector2.zero);
            var later = CreateRoot("later");
            CreateButton(later, "button", Vector2.zero);
            _navigation.Add(_lifetime, older);
            _navigation.Add(_lifetime, later);

            _navigation.OnUpdate(0f);
            UnityEngine.Object.DestroyImmediate(later.gameObject);
            _navigation.OnUpdate(0f);

            Assert.AreSame(olderButton.gameObject, Selected());
        }

        [Test]
        public void Overlay_OverWindowsAddedAfterIt()
        {
            Setup();

            var overlay = CreateRoot("overlay");
            var overlayButton = CreateButton(overlay, "button", Vector2.zero);
            var window = CreateRoot("window");
            var windowButton = CreateButton(window, "button", Vector2.zero);
            _navigation.AddOverlay(_lifetime, overlay);
            _navigation.Add(_lifetime, window);

            _navigation.OnUpdate(0f);
            Assert.AreSame(overlayButton.gameObject, Selected());

            overlay.gameObject.SetActive(false);
            _navigation.OnUpdate(0f);
            Assert.AreSame(windowButton.gameObject, Selected());
        }

        [Test]
        public void Overlay_LifetimeEnded_TheWindowBelow()
        {
            Setup();

            var overlayLifetime = new Lifetime();
            var overlay = CreateRoot("overlay");
            CreateButton(overlay, "button", Vector2.zero);
            var window = CreateRoot("window");
            var windowButton = CreateButton(window, "button", Vector2.zero);
            _navigation.Add(_lifetime, window);
            _navigation.AddOverlay(overlayLifetime, overlay);

            _navigation.OnUpdate(0f);
            overlayLifetime.Terminate();
            _navigation.OnUpdate(0f);

            Assert.AreSame(windowButton.gameObject, Selected());
        }

        [Test]
        public void Memory_PanelHides_BackToTheButtonThatOpenedIt()
        {
            Setup();

            var root = CreateRoot("window");
            CreateButton(root, "first", Vector2.zero);
            var opener = CreateButton(root, "opener", new Vector2(0f, -200f));
            var panel = CreateRect(root, "panel", Vector2.zero, new Vector2(500f, 500f));
            var panelButton = CreateButton(panel, "button", Vector2.zero);
            _navigation.Add(_lifetime, root);

            Select(opener);
            _navigation.OnUpdate(0f);
            Select(panelButton);
            _navigation.OnUpdate(0f);

            panel.gameObject.SetActive(false);
            _navigation.OnUpdate(0f);

            Assert.AreSame(opener.gameObject, Selected());
        }

        [Test]
        public void Memory_WindowOnTopCloses_TheSelectionOfTheOneBelowComesBack()
        {
            Setup();

            var below = CreateRoot("below");
            CreateButton(below, "first", Vector2.zero);
            var second = CreateButton(below, "second", new Vector2(0f, -200f));
            var top = CreateRoot("top");
            CreateButton(top, "button", Vector2.zero);
            _navigation.Add(_lifetime, below);

            Select(second);
            _navigation.OnUpdate(0f);

            var topLifetime = new Lifetime();
            _navigation.Add(topLifetime, top);
            _navigation.OnUpdate(0f);
            topLifetime.Terminate();
            _navigation.OnUpdate(0f);

            Assert.AreSame(second.gameObject, Selected());
        }

        [Test]
        public void Memory_SelectedAgain_BecomesTheNewest()
        {
            Setup();

            var root = CreateRoot("window");
            var a = CreateButton(root, "a", Vector2.zero);
            var b = CreateButton(root, "b", new Vector2(0f, -200f));
            var c = CreateButton(root, "c", new Vector2(0f, -400f));
            _navigation.Add(_lifetime, root);

            foreach (var button in new[] { a, b, c, a })
            {
                Select(button);
                _navigation.OnUpdate(0f);
            }

            Select(null);
            _navigation.OnUpdate(0f);

            Assert.AreSame(a.gameObject, Selected());
        }

        [TestCase(8, "b0")]
        [TestCase(9, "first")]
        public void Memory_KeepsTheLastEight(int selections, string expected)
        {
            Setup();

            var root = CreateRoot("window");
            CreateButton(root, "first", Vector2.zero);
            var buttons = new List<Button>();

            for (var i = 0; i < selections; i++)
                buttons.Add(CreateButton(root, "b" + i, new Vector2(0f, -100f * (i + 1))));

            _navigation.Add(_lifetime, root);

            foreach (var button in buttons)
            {
                Select(button);
                _navigation.OnUpdate(0f);
            }

            for (var i = 1; i < buttons.Count; i++)
                buttons[i].gameObject.SetActive(false);

            _navigation.OnUpdate(0f);

            Assert.AreEqual(expected, Selected().name);
        }

        [Test]
        public void Navigation_NearestInEachDirection()
        {
            Setup();

            var root = CreateRoot("window");
            var centre = CreateButton(root, "centre", Vector2.zero);
            var up = CreateButton(root, "up", new Vector2(0f, 200f));
            CreateButton(root, "far up", new Vector2(0f, 400f));
            var down = CreateButton(root, "down", new Vector2(0f, -200f));
            var left = CreateButton(root, "left", new Vector2(-200f, 0f));
            var right = CreateButton(root, "right", new Vector2(200f, 0f));
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);

            var navigation = centre.navigation;
            Assert.AreSame(centre.gameObject, Selected());
            Assert.AreEqual(Navigation.Mode.Explicit, navigation.mode);
            Assert.AreSame(up, navigation.selectOnUp);
            Assert.AreSame(down, navigation.selectOnDown);
            Assert.AreSame(left, navigation.selectOnLeft);
            Assert.AreSame(right, navigation.selectOnRight);
        }

        [Test]
        public void Navigation_NothingInADirection_Null()
        {
            Setup();

            var root = CreateRoot("window");
            var button = CreateButton(root, "button", Vector2.zero);
            var below = CreateButton(root, "below", new Vector2(0f, -200f));
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);

            var navigation = button.navigation;
            Assert.AreSame(below, navigation.selectOnDown);
            Assert.IsTrue(navigation.selectOnUp == null);
            Assert.IsTrue(navigation.selectOnLeft == null);
            Assert.IsTrue(navigation.selectOnRight == null);
        }

        [Test]
        public void Navigation_OnlyCandidatesOfTheWindow()
        {
            Setup();

            var root = CreateRoot("window");
            var button = CreateButton(root, "button", Vector2.zero);
            var off = CreateButton(root, "not interactable", new Vector2(0f, -100f));
            off.interactable = false;
            var below = CreateButton(root, "below", new Vector2(0f, -300f));

            var other = CreateRoot("other");
            CreateButton(other, "of another window", new Vector2(0f, -100f));

            _navigation.Add(_lifetime, root);
            _navigation.OnUpdate(0f);

            Assert.AreSame(below, button.navigation.selectOnDown);
        }

        // The nearest point, not the centre: the wide row right under the cell, not the small one with its centre nearer.
        [Test]
        public void Navigation_WideRowRightUnder_IsNext()
        {
            Setup();

            var root = CreateRoot("window");
            var cell = CreateButton(root, "cell", new Vector2(-200f, 0f), new Vector2(100f, 50f));
            var row = CreateButton(root, "row", new Vector2(100f, -80f), new Vector2(800f, 40f));
            CreateButton(root, "further", new Vector2(-150f, -200f), new Vector2(100f, 40f));
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);

            Assert.AreSame(row, cell.navigation.selectOnDown);
        }

        // Straight: 175 along. Aside: 25 along and 140 sideways, 165 with the plain sum, 305 with the sideways doubled.
        [Test]
        public void Navigation_SidewaysCountsTwice()
        {
            Setup();

            var root = CreateRoot("window");
            var button = CreateButton(root, "button", Vector2.zero, new Vector2(100f, 50f));
            var straight = CreateButton(root, "straight", new Vector2(0f, -225f), new Vector2(100f, 50f));
            CreateButton(root, "aside", new Vector2(190f, -75f), new Vector2(100f, 50f));
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);

            Assert.AreSame(straight, button.navigation.selectOnDown);
        }

        [Test]
        public void Navigation_OverTheCurrentOne_FoundFromItsCentre()
        {
            Setup();

            var root = CreateRoot("window");
            var card = CreateButton(root, "card", Vector2.zero, new Vector2(300f, 300f));
            var back = CreateButton(root, "back", new Vector2(0f, -100f), new Vector2(100f, 50f));
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);

            Assert.AreSame(back, card.navigation.selectOnDown);
        }

        [Test]
        public void Navigation_SomethingAheadOfTheEdge_WinsOverTheOneOnTop()
        {
            Setup();

            var root = CreateRoot("window");
            var card = CreateButton(root, "card", Vector2.zero, new Vector2(300f, 300f));
            CreateButton(root, "back", new Vector2(0f, -100f), new Vector2(100f, 50f));
            var next = CreateButton(root, "next", new Vector2(0f, -400f), new Vector2(100f, 50f));
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);

            Assert.AreSame(next, card.navigation.selectOnDown);
        }

        [TestCase(Slider.Direction.LeftToRight)]
        [TestCase(Slider.Direction.RightToLeft)]
        public void Navigation_HorizontalSlider_KeepsLeftAndRight(Slider.Direction direction)
        {
            Setup();

            var root = CreateRoot("window");
            var slider = CreateSlider(root, direction);
            var above = CreateButton(root, "above", new Vector2(0f, 200f));
            CreateButton(root, "left", new Vector2(-200f, 0f));
            CreateButton(root, "right", new Vector2(200f, 0f));
            _navigation.Add(_lifetime, root);

            Select(slider);
            _navigation.OnUpdate(0f);

            var navigation = slider.navigation;
            Assert.AreSame(above, navigation.selectOnUp);
            Assert.IsTrue(navigation.selectOnLeft == null);
            Assert.IsTrue(navigation.selectOnRight == null);
        }

        [TestCase(Slider.Direction.BottomToTop)]
        [TestCase(Slider.Direction.TopToBottom)]
        public void Navigation_VerticalSlider_LeftAndRightMove(Slider.Direction direction)
        {
            Setup();

            var root = CreateRoot("window");
            var slider = CreateSlider(root, direction);
            var left = CreateButton(root, "left", new Vector2(-200f, 0f));
            var right = CreateButton(root, "right", new Vector2(200f, 0f));
            _navigation.Add(_lifetime, root);

            Select(slider);
            _navigation.OnUpdate(0f);

            Assert.AreSame(left, slider.navigation.selectOnLeft);
            Assert.AreSame(right, slider.navigation.selectOnRight);
        }

        [Test]
        public void Navigation_FollowsTheCandidatesEachFrame()
        {
            Setup();

            var root = CreateRoot("window");
            var button = CreateButton(root, "button", Vector2.zero);
            var below = CreateButton(root, "below", new Vector2(0f, -200f));
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);
            Assert.AreSame(below, button.navigation.selectOnDown);

            var nearer = CreateButton(root, "nearer", new Vector2(0f, -100f));
            _navigation.OnUpdate(0f);
            Assert.AreSame(nearer, button.navigation.selectOnDown);
        }

        [Test]
        public void Navigation_SelectionLeaves_OwnOneIsGivenBack()
        {
            Setup();

            var root = CreateRoot("window");
            var a = CreateButton(root, "a", Vector2.zero);
            a.navigation = new Navigation { mode = Navigation.Mode.Vertical, wrapAround = true };
            var b = CreateButton(root, "b", new Vector2(0f, -200f));
            _navigation.Add(_lifetime, root);

            // two frames on one selectable: the second one must not take the set navigation for its own
            _navigation.OnUpdate(0f);
            _navigation.OnUpdate(0f);
            Assert.AreEqual(Navigation.Mode.Explicit, a.navigation.mode);

            Select(b);
            _navigation.OnUpdate(0f);

            Assert.AreEqual(Navigation.Mode.Vertical, a.navigation.mode);
            Assert.IsTrue(a.navigation.wrapAround);
            Assert.AreEqual(Navigation.Mode.Explicit, b.navigation.mode);
        }

        [Test]
        public void Navigation_DestroyedSelected_NextOneIsSelected()
        {
            Setup();

            var root = CreateRoot("window");
            var a = CreateButton(root, "a", Vector2.zero);
            var b = CreateButton(root, "b", new Vector2(0f, -200f));
            _navigation.Add(_lifetime, root);

            _navigation.OnUpdate(0f);
            UnityEngine.Object.DestroyImmediate(a.gameObject);
            _navigation.OnUpdate(0f);

            Assert.AreSame(b.gameObject, Selected());
        }

        [Test]
        public void OtherScheme_SelectionIsDroppedAndComesBack()
        {
            Setup();

            var root = CreateRoot("window");
            CreateButton(root, "first", Vector2.zero);
            var second = CreateButton(root, "second", new Vector2(0f, -200f));
            _navigation.Add(_lifetime, root);

            Select(second);
            _navigation.OnUpdate(0f);

            _scheme.Set(InputSchemeType.KeyboardMouse);
            _navigation.OnUpdate(0f);

            Assert.IsNull(Selected());
            Assert.AreEqual(Navigation.Mode.Automatic, second.navigation.mode);

            _scheme.Set(InputSchemeType.Gamepad);
            _navigation.OnUpdate(0f);

            Assert.AreSame(second.gameObject, Selected());
        }

        [Test]
        public void OtherScheme_InputFieldKeepsItsFocus()
        {
            Setup();

            var root = CreateRoot("window");
            var field = CreateRect(root, "field", Vector2.zero, new Vector2(100f, 50f)).gameObject;
            field.AddComponent<TMP_InputField>();
            var button = CreateButton(root, "button", new Vector2(0f, -200f));
            _navigation.Add(_lifetime, root);

            _scheme.Set(InputSchemeType.KeyboardMouse);
            EventSystem.current.SetSelectedGameObject(field);
            _navigation.OnUpdate(0f);
            Assert.AreSame(field, Selected());

            _scheme.Set(InputSchemeType.Gamepad);
            _navigation.OnUpdate(0f);
            Assert.AreSame(button.gameObject, Selected(), "the gamepad does not stay in a field");
        }

        [Test]
        public void OtherScheme_NoWindow_ButtonIsDeselected()
        {
            Setup();

            var root = CreateRoot("not added");
            var button = CreateButton(root, "button", Vector2.zero);

            _scheme.Set(InputSchemeType.KeyboardMouse);
            Select(button);
            _navigation.OnUpdate(0f);

            Assert.IsNull(Selected());
        }

        // The press that has put the selection on a button does not press it: the submit of this frame and the next.
        [UnityTest]
        public IEnumerator NewSelection_SuppressesTheSubmitOfTwoFrames()
        {
            Setup();

            var root = CreateRoot("window");
            CreateButton(root, "button", Vector2.zero);
            _navigation.Add(_lifetime, root);

            var input = EventSystem.current.GetComponent<GamepadUIInput>();
            var submit = EventSystem.current.GetComponent<StandaloneInputModule>().submitButton;

            _gamepad.Press(GamepadButton.A);
            Assert.IsTrue(input.GetButtonDown(submit), "no selection was made yet");

            _navigation.OnUpdate(0f);
            Assert.IsFalse(input.GetButtonDown(submit), "the frame of the selection");

            yield return null;
            Assert.IsFalse(input.GetButtonDown(submit), "the frame after the selection");

            yield return null;
            Assert.IsTrue(input.GetButtonDown(submit), "the selection stands, the press is a press");

            // the selection only stays: nothing is suppressed
            _navigation.OnUpdate(0f);
            Assert.IsTrue(input.GetButtonDown(submit));
        }

        [Test]
        public void Scroll_SelectedBelowTheView_ContentMovesUp()
        {
            var (scroll, button) = SetupScroll(new Vector2(0f, -300f));
            scroll.velocity = new Vector2(0f, 50f);

            Select(button);
            _navigation.OnUpdate(0f);

            Assert.AreEqual(225f, scroll.content.anchoredPosition.y, 0.01f);
            Assert.AreEqual(Vector2.zero, scroll.velocity);
        }

        [Test]
        public void Scroll_SelectedAboveTheView_ContentMovesDown()
        {
            var (scroll, button) = SetupScroll(new Vector2(0f, 300f));
            scroll.velocity = new Vector2(0f, 50f);

            Select(button);
            _navigation.OnUpdate(0f);

            Assert.AreEqual(-225f, scroll.content.anchoredPosition.y, 0.01f);
            Assert.AreEqual(Vector2.zero, scroll.velocity);
        }

        [Test]
        public void Scroll_SelectedInTheView_NothingMoves()
        {
            var (scroll, button) = SetupScroll(Vector2.zero);
            scroll.velocity = new Vector2(0f, 50f);

            Select(button);
            _navigation.OnUpdate(0f);

            Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.01f);
            Assert.AreEqual(new Vector2(0f, 50f), scroll.velocity);
        }

        [Test]
        public void Scroll_NotVertical_NothingMoves()
        {
            var (scroll, button) = SetupScroll(new Vector2(0f, -300f));
            scroll.vertical = false;

            Select(button);
            _navigation.OnUpdate(0f);

            Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.01f);
        }

        // Without a viewport the view is the rect of the scroll itself (300 high here, against 200 of the viewport).
        [Test]
        public void Scroll_NoViewport_TheViewIsTheScroll()
        {
            var (scroll, button) = SetupScroll(new Vector2(0f, -300f));
            scroll.viewport = null;

            Select(button);
            _navigation.OnUpdate(0f);

            Assert.AreEqual(175f, scroll.content.anchoredPosition.y, 0.01f);
        }

        private void Setup()
        {
            var events = Create("events");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();

            _navigation = CreateNavigation();
            _navigation.OnSetup(_lifetime);
        }

        private UINavigation CreateNavigation()
        {
            return new UINavigation(new NoUpdater(), new Scheme(_scheme), _gamepad, _options);
        }

        private (ScrollRect, Button) SetupScroll(Vector2 buttonPosition)
        {
            Setup();

            var root = CreateRoot("window");
            var scroll = CreateRect(root, "scroll", Vector2.zero, new Vector2(300f, 300f));
            var viewport = CreateRect(scroll, "viewport", Vector2.zero, new Vector2(200f, 200f));
            var content = CreateRect(viewport, "content", Vector2.zero, new Vector2(200f, 1000f));
            var button = CreateButton(content, "button", buttonPosition);

            var rect = scroll.gameObject.AddComponent<ScrollRect>();
            rect.viewport = viewport;
            rect.content = content;
            rect.vertical = true;
            rect.horizontal = false;

            _navigation.Add(_lifetime, root);

            return (rect, button);
        }

        private static GameObject Selected()
        {
            return EventSystem.current.currentSelectedGameObject;
        }

        private static void Select(Selectable selectable)
        {
            EventSystem.current.SetSelectedGameObject(selectable == null ? null : selectable.gameObject);
        }

        private RectTransform CreateRoot(string name)
        {
            var root = Create(name);
            var rect = root.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2000f, 2000f);

            // not an overlay: its size and scale would come from the screen of the run
            root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            return rect;
        }

        private static RectTransform CreateRect(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Button CreateButton(RectTransform parent, string name, Vector2 position)
        {
            return CreateButton(parent, name, position, new Vector2(100f, 50f));
        }

        private static Button CreateButton(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            return CreateRect(parent, name, position, size).gameObject.AddComponent<Button>();
        }

        private static Slider CreateSlider(RectTransform parent, Slider.Direction direction)
        {
            var slider = CreateRect(parent, "slider", Vector2.zero, new Vector2(100f, 50f)).gameObject.AddComponent<Slider>();
            slider.direction = direction;
            return slider;
        }

        private GameObject Create(string name)
        {
            var created = new GameObject(name);
            _created.Add(created);
            return created;
        }

        private class Scheme : IInputScheme
        {
            public Scheme(IViewableProperty<InputSchemeType> current)
            {
                Current = current;
            }

            public IViewableProperty<InputSchemeType> Current { get; }
        }

        private class FakeGamepad : IGamepadReader
        {
            private GamepadState _state;

            public GamepadState State => _state;
            public Vector2 LeftStick => _state.LeftStick;
            public Vector2 RightStick => _state.RightStick;
            public float LeftTrigger => _state.LeftTrigger;
            public float RightTrigger => _state.RightTrigger;
            public bool WasAnyPressed => _state.Buttons != 0;

            public void Press(GamepadButton button)
            {
                _state.SetPressed(button, true);
            }

            public bool IsPressed(GamepadButton button) => _state.IsPressed(button);
            public bool WasPressed(GamepadButton button) => _state.IsPressed(button);
            public bool WasReleased(GamepadButton button) => false;
        }
    }
}
