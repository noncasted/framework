using NUnit.Framework;

namespace Internal.Tests
{
    public class ButtonSoundsTests
    {
        [Test]
        public void Bind_HooksLiveOnTheLifetime()
        {
            var lifetime = new Lifetime();
            var clicks = 0;
            var hovers = 0;

            ButtonSounds.Bind(lifetime, () => clicks++, () => hovers++);

            ButtonSounds.Clicked.Invoke();
            ButtonSounds.Hovered.Invoke();

            Assert.AreEqual(1, clicks);
            Assert.AreEqual(1, hovers);

            lifetime.Terminate();

            Assert.IsNull(ButtonSounds.Clicked);
            Assert.IsNull(ButtonSounds.Hovered);
        }
    }
}
