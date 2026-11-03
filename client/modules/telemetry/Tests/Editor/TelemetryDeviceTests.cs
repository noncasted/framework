using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Global.Telemetry.Tests
{
    public class TelemetryDeviceTests
    {
        private static readonly string[] _closedList =
        {
            "Platform", "BuildVersion", "Browser", "BrowserMajor", "Os", "OsMajor", "Gpu", "Cores", "MemoryClass",
            "ScreenWidth", "ScreenHeight", "Dpr",
        };

        [Test]
        public void Device_HasOnlyTheClosedList()
        {
            CollectionAssert.AreEquivalent(_closedList, typeof(TelemetryDevice).GetProperties().Select(property => property.Name));
        }

        [Test]
        public void UnitySource_ReadsTheBase()
        {
            var device = new UnityTelemetryDeviceSource().Read();

            Assert.IsNotNull(device.Platform);
            Assert.IsNotNull(device.Cores);
        }

        private static IEnumerable<TestCaseData> OperatingSystems()
        {
            yield return new TestCaseData("Android OS 14 / API-34 (UP1A.231005.007/G991BXXU9EWK1)", "Android OS", 14);
            yield return new TestCaseData("Windows 11  (10.0.22631) 64bit", "Windows", 11);
            yield return new TestCaseData("Mac OS X 10_15_7", "Mac OS X", 10);
            yield return new TestCaseData("iOS 17.1.2", "iOS", 17);
            yield return new TestCaseData("Arch Linux 7.2.3-arch1-3 64bit", "Arch Linux", 7);
            yield return new TestCaseData("Linux", "Linux", null);
            yield return new TestCaseData("", null, null);
            yield return new TestCaseData("123", null, 123);
        }

        [TestCaseSource(nameof(OperatingSystems))]
        public void ProductVersion_KeepsNameAndMajorOnly(string text, string name, int? major)
        {
            var version = TelemetryProductVersion.Parse(text);

            Assert.AreEqual(name, version.Name);
            Assert.AreEqual(major, version.Major);
        }

        [TestCase(0, null)]
        [TestCase(-1, null)]
        [TestCase(100, 0.25)]
        [TestCase(512, 0.5)]
        [TestCase(1024, 1d)]
        [TestCase(3800, 4d)]
        [TestCase(7800, 8d)]
        [TestCase(16384, 16d)]
        [TestCase(9000000, 1024d)]
        public void MemoryClass_RoundsToPowerOfTwo(int megabytes, double? expected)
        {
            Assert.AreEqual(expected, UnityTelemetryDeviceSource.MemoryClass(megabytes));
        }

        [TestCase(RuntimePlatform.Android, 420f, 2.625d)]
        [TestCase(RuntimePlatform.IPhonePlayer, 326f, 2d)]
        [TestCase(RuntimePlatform.Android, 0f, null)]
        [TestCase(RuntimePlatform.LinuxPlayer, 96f, null)]
        [TestCase(RuntimePlatform.WindowsPlayer, 144f, null)]
        public void Dpr_OnlyWhereTheBaseOfTheDpiIsKnown(RuntimePlatform platform, float dpi, double? expected)
        {
            Assert.AreEqual(expected, UnityTelemetryDeviceSource.Dpr(platform, dpi));
        }
    }
}
