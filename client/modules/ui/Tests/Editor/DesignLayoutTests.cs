using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Global.UI.Tests
{
    public class DesignLayoutTests
    {
        [Test]
        public void NewLayout_HasPadding()
        {
            var gameObject = new GameObject("layout", typeof(RectTransform));

            try
            {
                Assert.IsNotNull(gameObject.AddComponent<DesignContainer>().Padding);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        // No padding is an error of the caller, not a zero padding.
        [Test]
        public void SetPaddingNull_RebuildThrows()
        {
            var gameObject = new GameObject("layout", typeof(RectTransform));
            var child = new GameObject("child", typeof(RectTransform));
            child.transform.SetParent(gameObject.transform, false);

            try
            {
                var layout = gameObject.AddComponent<DesignContainer>();
                layout.SetPadding(null);

                Assert.Throws<NullReferenceException>(layout.Rebuild);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
