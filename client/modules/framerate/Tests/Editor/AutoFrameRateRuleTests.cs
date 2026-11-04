using NUnit.Framework;

namespace Global.FrameRate.Tests
{
    // The decision of the automatic frame rate on a made-up time line: frames of a given work at a given rate.
    public class AutoFrameRateRuleTests
    {
        private AutoFrameRateOptions _options;
        private AutoFrameRateRule _rule;
        private double _now;
        private int _switches;

        [SetUp]
        public void SetUp()
        {
            _options = new AutoFrameRateOptions();
            _now = 100d;
            _switches = 0;
        }

        [Test]
        public void TheOptionsAreTheOnesOfTheTask()
        {
            Assert.That(_options.High, Is.EqualTo(60));
            Assert.That(_options.Low, Is.EqualTo(30));
            Assert.That(_options.DropWorkMs, Is.EqualTo(12f));
            Assert.That(_options.RiseWorkMs, Is.EqualTo(6f));
            Assert.That(_options.DropSeconds, Is.InRange(5f, 10f));
            Assert.That(_options.RiseSeconds, Is.EqualTo(30f));
        }

        [Test]
        public void HeavyFramesDropToTheLowRateAfterTheHold()
        {
            Start(false);

            Run(_options.DropSeconds - 1.5f, 60, 14d);
            Assert.That(_rule.Target, Is.EqualTo(60), "Not before the hold");

            // The hold is counted in whole windows (a window closes on the first frame past it): it ends within two windows.
            Run(1.5f + 2f * _options.WindowSeconds, 60, 14d);
            Assert.That(_rule.Target, Is.EqualTo(30));
            Assert.That(_switches, Is.EqualTo(1));
        }

        [Test]
        public void LightFramesAtTheLowRateRiseAfterTheHold()
        {
            Start(true);
            Assert.That(_rule.Target, Is.EqualTo(30), "The remembered decision starts the rule");

            Run(_options.RiseSeconds - 1.5f, 30, 4d);
            Assert.That(_rule.Target, Is.EqualTo(30), "Not before the hold");

            Run(1.5f + 2f * _options.WindowSeconds, 30, 4d);
            Assert.That(_rule.Target, Is.EqualTo(60));
            Assert.That(_switches, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorkBetweenTheThresholdsKeepsTheRate(bool isLow)
        {
            Start(isLow);

            Run(300f, isLow == true ? 30 : 60, 9d);

            Assert.That(_switches, Is.Zero);
            Assert.That(_rule.IsLow, Is.EqualTo(isLow));
        }

        [Test]
        public void AWindowOfLightFramesStartsTheHoldAnew()
        {
            Start(false);

            for (var i = 0; i < 10; i++)
            {
                Run(_options.DropSeconds - 2f, 60, 14d);
                Run(1.5f, 60, 4d);
            }

            Assert.That(_switches, Is.Zero);
        }

        [Test]
        public void AShortSpikeDoesNotDrop()
        {
            Start(false);

            for (var i = 0; i < 20; i++)
            {
                Run(3f, 60, 4d);
                Frame(1d / 60d, 400d);
            }

            Assert.That(_switches, Is.Zero);
        }

        [Test]
        public void AGapInTheFramesStartsTheHoldAnew()
        {
            Start(false);

            Run(_options.DropSeconds - 2f, 60, 14d);
            Frame(_options.WindowSeconds + 2d, 14d);
            Run(_options.DropSeconds - 2f, 60, 14d);

            Assert.That(_switches, Is.Zero);
        }

        // A device that heats up and cools down in waves: one change per wave, never one per window.
        [Test]
        public void WavesSwitchOncePerWave()
        {
            Start(false);

            for (var i = 0; i < 4; i++)
            {
                Run(20f, _rule.Target, 20d);
                Run(40f, _rule.Target, 3d);
            }

            Assert.That(_switches, Is.EqualTo(8));
            Assert.That(_rule.Target, Is.EqualTo(60));
        }

        private void Start(bool isLow)
        {
            _rule = new AutoFrameRateRule(_options, isLow);
        }

        private void Run(float seconds, int rate, double workMs)
        {
            var frames = (int)(seconds * rate);

            for (var i = 0; i < frames; i++)
                Frame(1d / rate, workMs);
        }

        private void Frame(double interval, double workMs)
        {
            _now += interval;

            if (_rule.Feed(_now, workMs) == true)
                _switches++;
        }
    }
}
