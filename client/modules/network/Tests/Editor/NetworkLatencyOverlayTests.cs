using System.Reflection;
using NUnit.Framework;

namespace Internal.Tests
{
    public class NetworkLatencyOverlayTests
    {
        // The scene of an EditMode test has no EventSystem. A scene without one has no text field in focus: N toggles the
        // overlay instead of throwing.
        [Test]
        public void IsTyping_WithoutEventSystem_False()
        {
            var isTyping = typeof(NetworkLatencyOverlay).GetMethod("IsTyping", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsFalse((bool)isTyping.Invoke(null, null));
        }
    }
}
