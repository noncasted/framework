using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Internal.Tests
{
    // The lifetime of an object follows the Unity messages of its component, so it is tested in play mode.
    public class GameObjectLifetimeTests
    {
        [UnityTest]
        public IEnumerator HiddenBeforeStart_LifetimeIsLive_EndsWhenTheShownObjectIsHidden()
        {
            var gameObject = new GameObject("Hidden");
            gameObject.SetActive(false);
            var behaviour = gameObject.AddComponent<TestBehaviour>();

            var lifetime = behaviour.GetObjectLifetime();

            Assert.IsFalse(lifetime.IsTerminated);

            gameObject.SetActive(true);
            yield return null;

            Assert.IsFalse(lifetime.IsTerminated);
            Assert.AreSame(lifetime, behaviour.GetObjectLifetime());

            gameObject.SetActive(false);

            Assert.IsTrue(lifetime.IsTerminated);

            Object.Destroy(gameObject);
        }

        // Unity sends no message to an object that has never been active: the lifetime still ends with it.
        [UnityTest]
        public IEnumerator NeverShown_Destroyed_LifetimeEnds()
        {
            var gameObject = new GameObject("Hidden");
            gameObject.SetActive(false);
            var lifetime = gameObject.AddComponent<TestBehaviour>().GetObjectLifetime();

            Object.Destroy(gameObject);
            yield return null;
            yield return null;
            yield return null;

            Assert.IsTrue(lifetime.IsTerminated, "the subscriptions of a screen never shown outlive it");
        }

        [UnityTest]
        public IEnumerator HiddenBeforeStart_ListenClick_WorksWhenShown()
        {
            var gameObject = new GameObject("Hidden");
            gameObject.SetActive(false);
            var button = gameObject.AddComponent<Button>();

            var clicks = 0;
            button.ListenClick(() => clicks++);

            gameObject.SetActive(true);
            yield return null;

            button.onClick.Invoke();

            Assert.AreEqual(1, clicks);

            Object.Destroy(gameObject);
        }

        [UnityTest]
        public IEnumerator Active_Disabled_LifetimeEnds()
        {
            var gameObject = new GameObject("Active");
            var behaviour = gameObject.AddComponent<TestBehaviour>();
            yield return null;

            var lifetime = behaviour.GetObjectLifetime();
            gameObject.SetActive(false);

            Assert.IsTrue(lifetime.IsTerminated);

            Object.Destroy(gameObject);
        }

        [UnityTest]
        public IEnumerator StartedThenHidden_DestroyedWhileHidden_LifetimeEnds()
        {
            var gameObject = new GameObject("Started");
            var behaviour = gameObject.AddComponent<TestBehaviour>();
            var shown = behaviour.GetObjectLifetime();
            yield return null;

            gameObject.SetActive(false);

            Assert.IsTrue(shown.IsTerminated);

            var lifetime = behaviour.GetObjectLifetime();

            Assert.IsFalse(lifetime.IsTerminated);

            Object.Destroy(gameObject);
            yield return null;

            Assert.IsTrue(lifetime.IsTerminated);
        }

        [UnityTest]
        public IEnumerator HiddenBeforeStart_ShownThenDestroyed_LifetimeEnds()
        {
            var gameObject = new GameObject("Hidden");
            gameObject.SetActive(false);
            var behaviour = gameObject.AddComponent<TestBehaviour>();

            var lifetime = behaviour.GetObjectLifetime();

            gameObject.SetActive(true);
            Object.Destroy(gameObject);
            yield return null;

            Assert.IsTrue(lifetime.IsTerminated);
        }
    }
}
