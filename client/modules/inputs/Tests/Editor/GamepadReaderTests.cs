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
