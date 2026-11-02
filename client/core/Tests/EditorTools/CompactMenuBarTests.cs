using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.EditorTests
{
    public class CompactMenuBarTests
    {
        // Batchmode has no main toolbar: a start there would fail on the missing overlay at every domain load: what the
        // start of a domain load leaves for the next editor tick.
        [Test]
        public void Batchmode_StartsNothing()
        {
            if (Application.isBatchMode == false)
                Assert.Ignore("An editor with a window has the toolbar");

            var pending = EditorApplication.delayCall;
            EditorApplication.delayCall = null;

            try
            {
                CompactMenuBar.Start();

                Assert.IsNull(EditorApplication.delayCall);
            }
            finally
            {
                EditorApplication.delayCall = pending;
            }
        }
    }
}
