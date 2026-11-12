using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Internal.Tests
{
    // The click of a button follows the lifetime of its object, so it is tested in play mode.
    public class ButtonLifetimeTests
    {
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
    }
}
