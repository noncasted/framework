using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Global.Telemetry.Tests
{
    // The same corpus as the Scrubber of the server: both sides must cut the same things.
    public class TelemetryScrubberTests
    {
        private const int _textOfTheStackLimit = TelemetryLimits.MaxStackLength;

        private static JArray ReadCorpus()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../../../backend/Tests/ClientTelemetry/Golden/pii_corpus.json"));

            return (JArray)JObject.Parse(File.ReadAllText(path))["cases"];
        }

        [Test]
        public void Corpus_NothingForbiddenLeft_TemplateKept()
        {
            var cases = ReadCorpus();
            var failures = new List<string>();

            Assert.Greater(cases.Count, 50);

            foreach (var item in cases)
            {
                var scrubbed = TelemetryScrubber.Scrub((string)item["text"]);

                foreach (var forbidden in item["forbidden"].Select(value => (string)value))
                {
                    if (scrubbed.Contains(forbidden))
                        failures.Add($"{item["name"]}: '{forbidden}' left in: {scrubbed}");
                }

                foreach (var kept in item["kept"].Select(value => (string)value))
                {
                    if (scrubbed.Contains(kept) == false)
                        failures.Add($"{item["name"]}: '{kept}' lost in: {scrubbed}");
                }
            }

            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void Scrub_RunTwice_NothingForbiddenComesBack()
        {
            foreach (var item in ReadCorpus())
            {
                var once = TelemetryScrubber.Scrub((string)item["text"]);
                var twice = TelemetryScrubber.Scrub(once);

                foreach (var forbidden in item["forbidden"].Select(value => (string)value))
                    Assert.IsFalse(twice.Contains(forbidden), $"{item["name"]}: {twice}");
            }
        }

        // The texts where a backtracking engine is at its worst, at the longest size that reaches the scrubber.
        [TestCase("a")]
        [TestCase("/")]
        [TestCase("a/")]
        [TestCase("a=")]
        [TestCase("a@")]
        [TestCase("a.")]
        [TestCase("1.")]
        [TestCase("1:")]
        [TestCase("% ")]
        [TestCase("< ")]
        [TestCase("{ ")]
        [TestCase("\" ")]
        [TestCase("token ")]
        [TestCase("token")]
        [TestCase("name:")]
        public void Scrub_WorstCaseText_StaysFast(string unit)
        {
            var text = string.Concat(Enumerable.Repeat(unit, _textOfTheStackLimit / unit.Length));
            var withOpening = "<" + text;

            AssertCleanedInTime(text);
            AssertCleanedInTime(withOpening);
        }

        // The longest run that is not replaced beforehand, again and again.
        [TestCase('/')]
        [TestCase('a')]
        [TestCase('.')]
        [TestCase('%')]
        [TestCase('=')]
        [TestCase('@')]
        public void Scrub_RunsJustUnderTheLimit_StaysFast(char symbol)
        {
            var run = new string(symbol, 159) + " ";
            var text = string.Concat(Enumerable.Repeat(run, _textOfTheStackLimit / run.Length));

            AssertCleanedInTime(text);
        }

        // Through the cleaner: a text over the timeout of an expression is a normal way out, it ends with a placeholder.
        private static void AssertCleanedInTime(string text)
        {
            var cleaner = new TelemetryTextCleaner();
            cleaner.BeginPass();

            var watch = Stopwatch.StartNew();
            var isClean = cleaner.TryCleanStack(text, out var clean);
            watch.Stop();

            Assert.IsTrue(isClean);
            Assert.IsNotNull(clean);
            Assert.Less(watch.ElapsedMilliseconds, 1000, $"{watch.ElapsedMilliseconds} ms");
        }

        [Test]
        public void Scrub_LongUnbrokenRun_Replaced()
        {
            var secret = new string('q', 5000);

            var scrubbed = TelemetryScrubber.Scrub("Lost " + secret + " here");

            StringAssert.DoesNotContain("qqq", scrubbed);
            StringAssert.StartsWith("Lost ", scrubbed);
            StringAssert.EndsWith(" here", scrubbed);
        }
    }
}
