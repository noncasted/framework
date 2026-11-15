using NUnit.Framework;

namespace Internal.EditorTests
{
    // The cap of an unfocused Play window must not take the limit of the game (settings, ?fps, the auto rule) away.
    public class PlayModeFrameCapTests
    {
        private const int Cap = 60;

        [Test]
        public void Focused_WritesNothing()
        {
            Assert.IsNull(new PlayModeFrameCap(Cap).OnFocus(true, 30));
        }

        [Test]
        public void Unfocused_Unlimited_IsCapped_AndRestoredOnFocus()
        {
            var cap = new PlayModeFrameCap(Cap);

            Assert.AreEqual(Cap, cap.OnFocus(false, -1));
            Assert.AreEqual(-1, cap.OnFocus(true, Cap));
        }

        [Test]
        public void Unfocused_AboveCap_IsRestoredToTheGameValue()
        {
            var cap = new PlayModeFrameCap(Cap);

            Assert.AreEqual(Cap, cap.OnFocus(false, 120));
            Assert.AreEqual(120, cap.OnFocus(true, Cap));
        }

        // The choice of 30 in the settings survives a click into another window and back.
        [Test]
        public void Unfocused_BelowCap_WritesNothing()
        {
            var cap = new PlayModeFrameCap(Cap);

            Assert.IsNull(cap.OnFocus(false, 30));
            Assert.IsNull(cap.OnFocus(true, 30));
        }

        [Test]
        public void GameChangedTheLimitWhileCapped_IsKept()
        {
            var cap = new PlayModeFrameCap(Cap);

            cap.OnFocus(false, -1);

            Assert.IsNull(cap.OnFocus(true, 30));
        }

        [Test]
        public void RepeatedUnfocus_KeepsTheFirstReplacedValue()
        {
            var cap = new PlayModeFrameCap(Cap);

            cap.OnFocus(false, -1);

            Assert.IsNull(cap.OnFocus(false, Cap));
            Assert.AreEqual(-1, cap.OnFocus(true, Cap));
        }
    }
}
