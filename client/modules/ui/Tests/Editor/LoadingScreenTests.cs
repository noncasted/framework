using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Global.UI.Tests
{
    public class LoadingScreenTests
    {
        private GameObject _object;
        private CanvasGroup _group;
        private LoadingScreen _screen;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("loading", typeof(RectTransform));
            _group = _object.AddComponent<CanvasGroup>();
            var animation = _object.AddComponent<LoadingScreenAnimation>();
            Set(animation, "_sequence", Array.Empty<Sprite>());

            _screen = _object.AddComponent<LoadingScreen>();
            var curve = new Internal.Curve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            Set(_screen, "_group", _group);
            Set(_screen, "_animation", animation);
            Set(_screen, "_curve", curve);
            Set(_screen, "_curveInstance", curve.CreateInstance());
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_object);
        }

        // Shown at once stays shown: the next update does not start the fade in from the start of the curve.
        [Test]
        public void ShowInstantly_NextUpdate_StaysShown()
        {
            _object.SetActive(false);

            _screen.ShowInstantly();

            Assert.IsTrue(_object.activeSelf, "the screen is not shown");
            _screen.OnUpdate(0.01f);
            Assert.AreEqual(1f, _group.alpha, 0.001f);
        }

        // Hide during the fade in: nothing is going to finish the fade in any more.
        [UnityTest]
        public IEnumerator Show_HideDuringTheFade_WaitEnds() => UniTask.ToCoroutine(async () =>
        {
            var show = _screen.Show();
            _screen.OnUpdate(0.1f);

            _screen.Hide();

            for (var i = 0; i < 5; i++)
            {
                _screen.OnUpdate(0.1f);
                await UniTask.Yield();
            }

            Assert.AreEqual(UniTaskStatus.Succeeded, show.Status, "Show hangs after Hide");
        });

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
