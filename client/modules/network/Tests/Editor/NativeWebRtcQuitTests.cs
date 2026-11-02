using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Internal.Tests
{
    public class NativeWebRtcQuitTests
    {
        private static readonly Type LibraryType = typeof(IWebRtcPeer).Assembly.GetType("Internal.NativeWebRtcLibrary", true);

        private class SilentEvents : IWebRtcPeerEvents
        {
            public void OnLocalDescription(string type, string sdp)
            {
            }

            public void OnLocalCandidate(string candidate, string mid)
            {
            }

            public void OnOpen()
            {
            }

            public void OnMessage(byte[] bytes)
            {
            }

            public void OnLost(string reason)
            {
            }
        }

        // A connection alive at exit is destroyed by the static destructors of the library, which can crash on the way out
        // (code 139 of the editor). The handler of the quit must delete it before that.
        [Test]
        public void Quit_DeletesTheLivePeers()
        {
            var peer = WebRtcPeers.Create(new WebRtcPeerOptions(Array.Empty<string>(), TimeSpan.FromSeconds(10)), new SilentEvents());

            try
            {
                var handlers = QuittingHandlers().Where(handler => handler.Method.DeclaringType == LibraryType).ToArray();

                Assert.IsNotEmpty(handlers, "nothing deletes the peers when the editor quits");

                foreach (var handler in handlers)
                    handler();

                Assert.Throws<InvalidOperationException>(() => peer.CreateChannel("session"));
            }
            finally
            {
                peer.Dispose();
            }
        }

        // EditorApplication.quitting has no public way to read it. A newer editor that changes this throws here.
        private static IEnumerable<Action> QuittingHandlers()
        {
            var quitting = typeof(EditorApplication).GetField("m_QuittingEvent", BindingFlags.Static | BindingFlags.NonPublic)
                                                    .GetValue(null);
            var enumerator = quitting.GetType().GetMethod("GetEnumerator").Invoke(quitting, null);
            var type = enumerator.GetType();

            while ((bool)type.GetMethod("MoveNext").Invoke(enumerator, null) == true)
                yield return (Action)type.GetProperty("Current").GetValue(enumerator);
        }
    }
}
