using System;
using Internal;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Global.Localization.Tests
{
    public class LocalizedLabelTests
    {
        // A lifetime longer than the label is a bug of the caller: it is seen, not hidden by a check in the label.
        [Test]
        public void LabelDestroyedBeforeTheLifetime_Throws()
        {
            var lifetime = new Lifetime();
            var gameObject = new GameObject("label", typeof(RectTransform));

            try
            {
                var label = new LocalizedLabel(lifetime, gameObject.AddComponent<TextMeshProUGUI>());
                Object.DestroyImmediate(gameObject);

                Assert.Throws<InvalidOperationException>(() => label.Set(LocalizedText.Raw("text")));
            }
            finally
            {
                lifetime.Terminate();

                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            }
        }
    }
}
