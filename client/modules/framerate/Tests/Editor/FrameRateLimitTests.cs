using Internal;
using NUnit.Framework;
using UnityEngine;

namespace Global.FrameRate.Tests
{
    // Which limit wins: ?fps= of the page, the fixed choice of the settings, the automatic rate on a phone, the rate of the
    // screen on a desktop; and the decision of the automatic rate kept on the device for the next start.
    public class FrameRateLimitTests
    {
        private Lifetime _lifetime;
        private int _targetBefore;
        private FakeSetting _settings;
        private FakeAuto _auto;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
            _targetBefore = Application.targetFrameRate;
            _settings = new FakeSetting();
            _auto = new FakeAuto();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
            Application.targetFrameRate = _targetBefore;
            PlatformOptions.SetMobileCheck(null);
            new AutoFrameRateMemory().Forget();
        }

        [Test]
        public void AutoOnAPhoneFollowsTheAutomaticRate()
        {
            Start(isMobile: true, request: null);

            Assert.That(_auto.Runs, Is.EqualTo(1));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));

            _auto.Switch(30);
            Assert.That(Application.targetFrameRate, Is.EqualTo(30));
        }

        [Test]
        public void AutoOnADesktopIsTheRateOfTheScreen()
        {
            Start(isMobile: false, request: null);

            Assert.That(_auto.Runs, Is.Zero);
            Assert.That(Application.targetFrameRate, Is.EqualTo(FrameRateLimit.Native));
        }

        [TestCase(FrameRateMode.Low, 30)]
        [TestCase(FrameRateMode.High, 60)]
        public void AFixedChoiceStopsTheAutomaticRate(FrameRateMode mode, int expected)
        {
            Start(isMobile: true, request: null);

            _settings.Set(mode);

            Assert.That(_auto.Running.IsTerminated, Is.True);
            Assert.That(Application.targetFrameRate, Is.EqualTo(expected));

            _auto.Switch(30);
            Assert.That(Application.targetFrameRate, Is.EqualTo(expected), "The stopped rate changes nothing");

            _settings.Set(FrameRateMode.Auto);
            Assert.That(_auto.Runs, Is.EqualTo(2));
        }

        [TestCase(45)]
        [TestCase(FrameRateLimit.Native)]
        public void TheRequestOfThePageWins(int request)
        {
            _settings.Set(FrameRateMode.Low);
            Start(isMobile: true, request: request);

            _settings.Set(FrameRateMode.Auto);

            Assert.That(_auto.Runs, Is.Zero);
            Assert.That(Application.targetFrameRate, Is.EqualTo(request));
        }

        [Test]
        public void TheDecisionIsKeptForTheNextStart()
        {
            var memory = new AutoFrameRateMemory();
            memory.Forget();
            Assert.That(memory.IsLow, Is.False, "A device starts at the high rate");

            memory.Remember(true);
            Assert.That(new AutoFrameRateRule(new AutoFrameRateOptions(), new AutoFrameRateMemory().IsLow).Target, Is.EqualTo(30));

            memory.Remember(false);
            Assert.That(new AutoFrameRateRule(new AutoFrameRateOptions(), memory.IsLow).Target, Is.EqualTo(60));
        }

        private void Start(bool isMobile, int? request)
        {
            PlatformOptions.SetMobileCheck(() => isMobile);

            var limit = new FrameRateLimit(new AutoFrameRateOptions(), _settings, new FakeRequest(request), _auto,
                new PlatformOptions());

            limit.OnSetupCompletion(_lifetime);
        }

        private class FakeRequest : IFrameRateRequest
        {
            public FakeRequest(int? limit)
            {
                _limit = limit;
            }

            private readonly int? _limit;

            public bool TryGet(out int limit)
            {
                limit = _limit ?? 0;
                return _limit != null;
            }
        }

        private class FakeAuto : IAutoFrameRate
        {
            private readonly ViewableProperty<int> _target = new();
            private readonly EventSource<int> _switched = new();

            public int Runs { get; private set; }
            public IReadOnlyLifetime Running { get; private set; }
            public IViewableProperty<int> Target => _target;
            public IEventSource<int> Switched => _switched;

            public void Run(IReadOnlyLifetime lifetime)
            {
                Runs++;
                Running = lifetime;
                _target.Set(60);
            }

            public void Switch(int target)
            {
                _target.Set(target);
                _switched.Invoke(target);
            }
        }

        private class FakeSetting : IFrameRateSetting
        {
            private readonly ViewableProperty<FrameRateMode> _frameRate = new();

            public IViewableProperty<FrameRateMode> FrameRate => _frameRate;

            public void Set(FrameRateMode mode) => _frameRate.Set(mode);
        }
    }
}
