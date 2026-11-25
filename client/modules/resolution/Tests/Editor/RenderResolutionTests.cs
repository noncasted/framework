using System.Collections.Generic;
using Internal;
using NUnit.Framework;

namespace Global.Resolution.Tests
{
    // The resolution of the settings is handed to the platform at the setup and on every change only, while the scope lives.
    public class RenderResolutionTests
    {
        private Lifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        [Test]
        public void ThePlatformGetsTheResolutionAtTheSetupAndOnEveryChange()
        {
            var setting = new FakeSetting();
            var pixelRatio = new FakePixelRatio();

            new RenderResolution(setting, pixelRatio, Logs.Of<RenderResolution>()).OnSetup(_lifetime);
            Assert.That(pixelRatio.Applied, Is.EqualTo(new[] { ResolutionMode.Auto }));

            setting.Set(ResolutionMode.High);
            setting.Set(ResolutionMode.High);
            setting.Set(ResolutionMode.Low);
            Assert.That(pixelRatio.Applied,
                Is.EqualTo(new[] { ResolutionMode.Auto, ResolutionMode.High, ResolutionMode.Low }),
                "An equal value is not handed again");

            _lifetime.Terminate();
            setting.Set(ResolutionMode.Auto);
            Assert.That(pixelRatio.Applied.Count, Is.EqualTo(3), "Nothing after the end of the scope");
        }

        private class FakePixelRatio : IRenderPixelRatio
        {
            public List<ResolutionMode> Applied { get; } = new();

            public void Apply(ResolutionMode mode) => Applied.Add(mode);
        }

        private class FakeSetting : IResolutionSetting
        {
            private readonly ViewableProperty<ResolutionMode> _resolution = new();

            public IViewableProperty<ResolutionMode> Resolution => _resolution;

            public void Set(ResolutionMode mode) => _resolution.Set(mode);
        }
    }
}
