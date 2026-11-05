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

        // A clone of Multiplayer Play Mode has no main toolbar either: the start in its editor failed on the missing
        // overlay at every domain load.
        [Test]
        public void CloneOfMultiplayerPlayMode_StartsNothing()
        {
            var pending = EditorApplication.delayCall;
            EditorApplication.delayCall = null;

            try
            {
                CompactMenuBar.Start(new[] { "Unity", CompactMenuBar.CloneArgument, "-vp-channel-name=vp-channel" }, false);

                Assert.IsNull(EditorApplication.delayCall);
            }
            finally
            {
                EditorApplication.delayCall = pending;
            }
        }
    }
}
