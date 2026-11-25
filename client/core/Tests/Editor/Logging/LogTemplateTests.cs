using System;
using NUnit.Framework;

namespace Internal.EditorTests
{
    public class LogTemplateTests
    {
        [Test]
        public void Format_PlaceholdersTakeTheArgumentsByOrder()
        {
            Assert.AreEqual("Joined 7 as host in 12 ms", LogTemplate.Format("Joined {Code} as {Role} in {Ms} ms", 7, "host", 12L));
        }

        [Test]
        public void Format_DoubledBracesAreBraces()
        {
            Assert.AreEqual("{x} 3 {}", LogTemplate.Format("{{x}} {Value} {{}}", 3));
        }

        [Test]
        public void Format_NumbersInTheInvariantCulture()
        {
            Assert.AreEqual("1.5 2.25 true", LogTemplate.Format("{A} {B} {C}", 1.5f, 2.25d, true));
        }

        [Test]
        public void Format_NullIsWritten()
        {
            Assert.AreEqual("null null", LogTemplate.Format("{A} {B}", (string)null, (object)null));
        }

        [Test]
        public void Format_OtherTypesByToString()
        {
            Assert.AreEqual("Warning (1, 2)", LogTemplate.Format("{Level} {Pair}", LogLevel.Warning, (1, 2)));
        }

        [Test]
        public void Format_WithoutPlaceholders_IsTheTemplateItself()
        {
            const string template = "Connected";

            Assert.AreSame(template, LogTemplate.Format(template));
        }

        [Test]
        public void Format_FourArguments()
        {
            Assert.AreEqual("1 2 3 4", LogTemplate.Format("{A} {B} {C} {D}", 1, 2, 3, 4));
        }

        [TestCase("{A} {B}")]
        [TestCase("no placeholder")]
        [TestCase("{A")]
        [TestCase("A}")]
        public void Format_PlaceholdersOtherThanTheArguments_Throw(string template)
        {
            Assert.Throws<FormatException>(() => LogTemplate.Format(template, 1));
        }
    }
}
