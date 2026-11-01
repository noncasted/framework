using System.Collections.Generic;
using Internal;
using NUnit.Framework;

namespace Global.Inputs.Tests
{
    public class BackInputTests
    {
        // Two windows are open: one press closes the one on top, and the next press the one under it.
        [Test]
        public void ListenBack_TwoWindows_OnePressClosesTheOneOnTop()
        {
            var back = new BackInput(null, new NoGamepadReader());
            var closed = new List<string>();
            var lower = new Lifetime();
            var upper = new Lifetime();

            back.ListenBack(lower, () => {
                closed.Add("lower");
                lower.Terminate();
            });

            back.ListenBack(upper, () => {
                closed.Add("upper");
                upper.Terminate();
            });

            Press(back);
            CollectionAssert.AreEqual(new[] { "upper" }, closed);

            Press(back);
            CollectionAssert.AreEqual(new[] { "upper", "lower" }, closed);

            Assert.IsFalse(back.HasTargets.Value);
        }

        [Test]
        public void ListenBack_NothingPressed_NotCalled()
        {
            var back = new BackInput(null, new NoGamepadReader());
            var calls = 0;
            back.ListenBack(new Lifetime(), () => calls++);

            back.OnPreUpdate(0f);

            Assert.AreEqual(0, calls);
        }

        private static void Press(BackInput back)
        {
            back.Press(BackInputKind.Back);
            back.OnPreUpdate(0f);
        }
    }
}
