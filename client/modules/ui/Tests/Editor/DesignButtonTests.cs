using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Global.UI.Tests
{
    // The look of a button follows the pointer. The order of the events is the one of StandaloneInputModule: a mouse
    // exits when it leaves, a touch enters on the press and exits right after the release.
    public class DesignButtonTests
    {
        private GameObject _object;
        private DesignElement _element;
        private DesignButton _button;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("button");
            _element = _object.AddComponent<DesignElement>();
            _button = _object.AddComponent<DesignButton>();
            typeof(DesignButton)
                .GetField("_element", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_button, _element);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        // A button without its Button is a broken prefab: seen when it is shown, not repaired at runtime.
        [Test]
        public void EnabledWithoutItsButton_Throws()
        {
            _object.AddComponent<Button>();
            Field("_button").SetValue(_button, null);

            var enable = typeof(DesignButton).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
            var exception = Assert.Throws<TargetInvocationException>(() => enable.Invoke(_button, null));

            Assert.IsInstanceOf<NullReferenceException>(exception.InnerException);
        }

        // The editor fills what is missing: a button whose element is set already gets its Button too.
        [Test]
        public void OnValidate_ElementSet_FillsTheButton()
        {
            var button = _object.AddComponent<Button>();
            Field("_button").SetValue(_button, null);

            typeof(DesignButton)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_button, null);

            Assert.AreEqual(button, Field("_button").GetValue(_button));
        }

        private static FieldInfo Field(string name)
        {
            return typeof(DesignButton).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        [Test]
        public void PressedLeftReleasedOutside_IsIdle()
        {
            _button.OnPointerEnter(null);
            _button.OnPointerDown(null);
            _button.OnPointerExit(null);
            _button.OnPointerUp(null);

            Assert.AreEqual(DesignElementState.Idle, _element.State.Value, "the button stays hovered without the pointer");
        }

        [Test]
        public void MouseClickInside_IsHovered()
        {
            _button.OnPointerEnter(null);
            _button.OnPointerDown(null);
            _button.OnPointerUp(null);

            Assert.AreEqual(DesignElementState.Hovered, _element.State.Value);
        }

        [Test]
        public void TouchClick_IsIdle()
        {
            _button.OnPointerEnter(null);
            _button.OnPointerDown(null);
            _button.OnPointerUp(null);
            _button.OnPointerExit(null);

            Assert.AreEqual(DesignElementState.Idle, _element.State.Value);
        }
    }
}
