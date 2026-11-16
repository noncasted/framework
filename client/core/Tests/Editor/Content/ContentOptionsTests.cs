using System;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    // The options of the content (ContentOptions): the scenes of a game saved before MaxFetches get its default, a limit below
    // one fetch is refused.
    public class ContentOptionsTests
    {
        [Test]
        public void MaxFetches_MissingFromTheData_IsTheDefault()
        {
            var options = JsonUtility.FromJson<ContentOptions>("{\"_name\":\"Game\"}");

            options.Validate();
            Assert.That(options.MaxFetches, Is.EqualTo(ContentOptions.DefaultMaxFetches));
        }

        [Test]
        public void MaxFetches_Zero_FailsTheValidation()
        {
            var options = new ContentOptions("Game", maxFetches: 0);

            Assert.Throws<InvalidOperationException>(options.Validate);
        }
    }
}
