using NUnit.Framework;

namespace Internal.EditorTests
{
    public class LogNamesTests
    {
        [Test]
        public void Of_NestedType_AfterADot()
        {
            Assert.AreEqual("Internal.EditorTests.LogNamesTests.Nested", LogNames.Of(typeof(Nested)));
        }

        [Test]
        public void Of_GenericType_ByTheShortNamesOfItsArguments()
        {
            Assert.AreEqual("Internal.EditorTests.LogNamesTests.Generic<Nested, Int32>", LogNames.Of(typeof(Generic<Nested, int>)));
            Assert.AreEqual("Generic<Nested, Int32>", LogNames.ShortOf(typeof(Generic<Nested, int>)));
        }

        [Test]
        public void ShortOf_IsTheNameOfTheType()
        {
            Assert.AreEqual("Nested", LogNames.ShortOf(typeof(Nested)));
        }

        private class Nested
        {
        }

        private class Generic<T1, T2>
        {
        }
    }
}
