using System;
using System.Linq;
using Internal;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Global.Inputs.Tests
{
    // The reader depends on what the project and the platform give: both are checked when it is built, and a platform
    // without a gamepad layout has its own reader.
    public class GamepadReaderTests
    {
        private InputOptions _options;

        [SetUp]
        public void SetUp()
        {
            _options = ScriptableObject.CreateInstance<InputOptions>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_options);
        }

        // Without the axes in ProjectSettings/InputManager.asset every read throws ArgumentException, each frame.
        [Test]
        public void Construct_AxesOfTheProject_MissingOnesNamedAtOnce()
        {
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
            var axes = manager.FindProperty("m_Axes");
            var names = Enumerable.Range(0, axes.arraySize)
                                  .Select(i => axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue)
                                  .ToHashSet();

            var hasAxes = Enumerable.Range(1, GamepadReader.RawAxisCount)
                                    .All(axis => names.Contains(GamepadReader.GetRawAxisName(axis)));

            if (hasAxes == true)
            {
                Assert.DoesNotThrow(() => new GamepadReader(_options));
                return;
            }

            var exception = Assert.Catch<InvalidOperationException>(() => new GamepadReader(_options));
            StringAssert.Contains("InputManager", exception.Message);
            StringAssert.Contains(GamepadReader.GetRawAxisName(1), exception.Message);
        }

        // Reads every axis of the layout of this platform through the Input Manager: a missing one throws here.
        [Test]
        public void ReadInput_EveryAxisOfTheLayout_IsRead()
        {
            var reader = new GamepadReader(_options);

            var state = reader.State;

            Assert.That(state.LeftTrigger, Is.InRange(0f, 1f));
            Assert.That(state.RightTrigger, Is.InRange(0f, 1f));
        }

        [Test]
        public void Substitute_TheStateComesFromTheSample()
        {
            var reader = new GamepadReader(_options);
            var lifetime = new Lifetime();
            var sample = new GamepadState();
            sample.SetLeftTrigger(0.25f);
            sample.SetPressed(GamepadButton.X, true);

            reader.Substitute(lifetime, () => sample);

            Assert.AreEqual(0.25f, reader.LeftTrigger);
            Assert.IsTrue(reader.IsPressed(GamepadButton.X));

            lifetime.Terminate();
        }

        [Test]
        public void Substitute_Twice_Throws()
        {
            var reader = new GamepadReader(_options);
            var lifetime = new Lifetime();

            reader.Substitute(lifetime, () => new GamepadState());

            Assert.Throws<InvalidOperationException>(() => reader.Substitute(lifetime, () => new GamepadState()));

            lifetime.Terminate();
        }

        // A next substitute is allowed only after the lifetime of the first one ended.
        [Test]
        public void Substitute_LifetimeEnded_CanBeSubstitutedAgain()
        {
            var reader = new GamepadReader(_options);
            var first = new Lifetime();
            var second = new Lifetime();

            reader.Substitute(first, () => new GamepadState());
            first.Terminate();

            Assert.DoesNotThrow(() => reader.Substitute(second, () => new GamepadState()));

            second.Terminate();
        }

        [TestCase(RuntimePlatform.WebGLPlayer)]
        [TestCase(RuntimePlatform.LinuxEditor)]
        [TestCase(RuntimePlatform.LinuxPlayer)]
        [TestCase(RuntimePlatform.WindowsPlayer)]
        [TestCase(RuntimePlatform.WindowsEditor)]
        public void Layout_KnownPlatforms(RuntimePlatform platform)
        {
            Assert.IsTrue(GamepadLayout.HasLayout(platform));
            Assert.DoesNotThrow(() => GamepadLayout.ForPlatform(platform));
        }

        [TestCase(RuntimePlatform.OSXEditor)]
        [TestCase(RuntimePlatform.OSXPlayer)]
        [TestCase(RuntimePlatform.Android)]
        [TestCase(RuntimePlatform.IPhonePlayer)]
        public void Layout_OtherPlatforms_HaveNone(RuntimePlatform platform)
        {
            Assert.IsFalse(GamepadLayout.HasLayout(platform), "AddInput fails while the scope is built on this platform");
        }

        [Test]
        public void Layout_OtherPlatform_Throws()
        {
            Assert.Throws<NotSupportedException>(() => GamepadLayout.ForPlatform(RuntimePlatform.Android));
        }

        [TestCase(RuntimePlatform.WebGLPlayer, 1, 2, 4, 5, 9, 10, 6, 7, 1f)]
        [TestCase(RuntimePlatform.WindowsPlayer, 1, 2, 4, 5, 9, 10, 6, 7, 1f)]
        [TestCase(RuntimePlatform.WindowsEditor, 1, 2, 4, 5, 9, 10, 6, 7, 1f)]
        [TestCase(RuntimePlatform.LinuxPlayer, 1, 2, 4, 5, 3, 6, 7, 8, -1f)]
        [TestCase(RuntimePlatform.LinuxEditor, 1, 2, 4, 5, 3, 6, 7, 8, -1f)]
        public void Layout_Axes(RuntimePlatform platform, int leftX, int leftY, int rightX, int rightY, int leftTrigger,
            int rightTrigger, int dpadX, int dpadY, float dpadYUp)
        {
            var layout = GamepadLayout.ForPlatform(platform);

            Assert.AreEqual(leftX, layout.LeftX);
            Assert.AreEqual(leftY, layout.LeftY);
            Assert.AreEqual(rightX, layout.RightX);
            Assert.AreEqual(rightY, layout.RightY);
            Assert.AreEqual(leftTrigger, layout.LeftTrigger);
            Assert.AreEqual(rightTrigger, layout.RightTrigger);
            Assert.AreEqual(dpadX, layout.DpadX);
            Assert.AreEqual(dpadY, layout.DpadY);
            Assert.AreEqual(dpadYUp, layout.DpadYUp);
        }

        [TestCase(RuntimePlatform.WebGLPlayer, 8, 9)]
        [TestCase(RuntimePlatform.WindowsPlayer, 8, 9)]
        [TestCase(RuntimePlatform.WindowsEditor, 8, 9)]
        [TestCase(RuntimePlatform.LinuxPlayer, 9, 10)]
        [TestCase(RuntimePlatform.LinuxEditor, 9, 10)]
        public void Layout_Buttons(RuntimePlatform platform, int leftStick, int rightStick)
        {
            var layout = GamepadLayout.ForPlatform(platform);

            Assert.AreEqual(KeyCode.JoystickButton0, layout.GetKey(GamepadButton.A));
            Assert.AreEqual(KeyCode.JoystickButton1, layout.GetKey(GamepadButton.B));
            Assert.AreEqual(KeyCode.JoystickButton2, layout.GetKey(GamepadButton.X));
            Assert.AreEqual(KeyCode.JoystickButton3, layout.GetKey(GamepadButton.Y));
            Assert.AreEqual(KeyCode.JoystickButton4, layout.GetKey(GamepadButton.LeftBumper));
            Assert.AreEqual(KeyCode.JoystickButton5, layout.GetKey(GamepadButton.RightBumper));
            Assert.AreEqual(KeyCode.JoystickButton6, layout.GetKey(GamepadButton.View));
            Assert.AreEqual(KeyCode.JoystickButton7, layout.GetKey(GamepadButton.Menu));
            Assert.AreEqual(KeyCode.JoystickButton0 + leftStick, layout.GetKey(GamepadButton.LeftStick));
            Assert.AreEqual(KeyCode.JoystickButton0 + rightStick, layout.GetKey(GamepadButton.RightStick));
        }

        [TestCase(GamepadButton.LeftTrigger)]
        [TestCase(GamepadButton.RightTrigger)]
        [TestCase(GamepadButton.DpadUp)]
        [TestCase(GamepadButton.DpadDown)]
        [TestCase(GamepadButton.DpadLeft)]
        [TestCase(GamepadButton.DpadRight)]
        public void Layout_TriggersAndDpad_AreNotJoystickButtons(GamepadButton button)
        {
            var layout = GamepadLayout.ForPlatform(RuntimePlatform.WebGLPlayer);

            Assert.Throws<ArgumentOutOfRangeException>(() => layout.GetKey(button));
        }

        // The bit of a button is its position in the enum: a sample built by a substitute of a game depends on it.
        [Test]
        public void Button_Order()
        {
            var expected = new[]
            {
                "A", "B", "X", "Y", "LeftBumper", "RightBumper", "LeftTrigger", "RightTrigger", "View", "Menu",
                "LeftStick", "RightStick", "DpadUp", "DpadDown", "DpadLeft", "DpadRight"
            };

            CollectionAssert.AreEqual(expected, Enum.GetNames(typeof(GamepadButton)));
        }

        [Test]
        public void State_ButtonsAreBits()
        {
            var state = new GamepadState();

            state.SetPressed(GamepadButton.X, true);
            state.SetPressed(GamepadButton.DpadRight, true);

            Assert.AreEqual((1 << 2) | (1 << 15), state.Buttons);
            Assert.IsTrue(state.IsPressed(GamepadButton.X));
            Assert.IsTrue(state.IsPressed(GamepadButton.DpadRight));
            Assert.IsFalse(state.IsPressed(GamepadButton.A));

            state.SetPressed(GamepadButton.X, false);

            Assert.AreEqual(1 << 15, state.Buttons);
            Assert.IsFalse(state.IsPressed(GamepadButton.X));
        }

        [Test]
        public void NoGamepad_NothingPressed()
        {
            var reader = new NoGamepadReader();

            Assert.IsFalse(reader.WasAnyPressed);
            Assert.AreEqual(Vector2.zero, reader.LeftStick);
            Assert.AreEqual(Vector2.zero, reader.RightStick);
            Assert.AreEqual(0f, reader.LeftTrigger);
            Assert.AreEqual(0f, reader.RightTrigger);
            Assert.IsFalse(reader.IsPressed(GamepadButton.A));
            Assert.IsFalse(reader.WasPressed(GamepadButton.A));
            Assert.IsFalse(reader.WasReleased(GamepadButton.A));
        }
    }
}
