using NUnit.Framework;

namespace Internal.EditorTests
{
    public class LogOptionsTests
    {
        [Test]
        public void LevelsOf_WithoutARule_TheDefaultsOfTheBuild()
        {
            var options = new LogOptions(LogLevel.Debug, LogLevel.Error, LogLevel.Warning);

            Assert.AreEqual((LogLevel.Debug, LogLevel.Warning), options.LevelsOf("Global.Network.Peer", true));
            Assert.AreEqual((LogLevel.Error, LogLevel.Warning), options.LevelsOf("Global.Network.Peer", false));
        }

        [Test]
        public void LevelsOf_TheLongestPrefixWins()
        {
            var options = new LogOptions(LogLevel.Info, LogLevel.Warning, LogLevel.None,
                new LogRule("Global", LogLevel.Warning, LogLevel.Warning),
                new LogRule("Global.Network", LogLevel.Debug, LogLevel.Info, LogLevel.Info),
                new LogRule("Global.Net", LogLevel.Error, LogLevel.Error));

            Assert.AreEqual((LogLevel.Debug, LogLevel.Info), options.LevelsOf("Global.Network.Peer", true));
            Assert.AreEqual((LogLevel.Info, LogLevel.Info), options.LevelsOf("Global.Network", false));
            Assert.AreEqual((LogLevel.Warning, LogLevel.None), options.LevelsOf("Global.Audio", true));
        }

        [Test]
        public void LevelsOf_APrefixMatchesWholeSegments()
        {
            var options = new LogOptions(LogLevel.Info, LogLevel.Warning, LogLevel.None,
                new LogRule("Global.Network", LogLevel.Debug, LogLevel.Debug));

            Assert.AreEqual(LogLevel.Info, options.LevelsOf("Global.NetworkTools", true).Shown);
        }
    }
}
