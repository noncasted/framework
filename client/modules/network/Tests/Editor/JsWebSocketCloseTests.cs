using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The browser may call onopen and onclose between two frames of the player (a server that accepts and closes at
    // once: 4001 of an outdated client). The socket did open, so Connect succeeds and the close is delivered with its
    // code. The functions of the jslib do not exist in the editor: the socket is built without them, the browser
    // callbacks are called as the browser does.
    public class JsWebSocketCloseTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
        private const int Id = 987654;

        [UnityTest]
        public IEnumerator OpenAndCloseBetweenFrames_ConnectSucceeds_CloseDelivered() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var socket = Create(lifetime);
            var instances = (Dictionary<int, JsWebSocket>)typeof(JsWebSocket).GetField("Instances", Flags).GetValue(null);
            instances[Id] = socket;

            try
            {
                var closed = new List<SocketCloseInfo>();
                socket.Closed.Advise(lifetime, closed.Add);
                SocketPump.Run(lifetime, () => Call(socket, "Deliver")).NoAwait();

                Call(null, "OnOpen", Id);
                Call(null, "OnClose", Id, 4001);

                await (UniTask)Call(socket, "WaitOpened");

                await UniTask.WaitUntil(() => closed.Count > 0).Timeout(TimeSpan.FromSeconds(5));
                Assert.AreEqual(4001, closed[0].Code);
            }
            finally
            {
                instances.Remove(Id);
                lifetime.Terminate();
            }
        });

        // A socket that never opened still fails Connect.
        [UnityTest]
        public IEnumerator CloseWithoutOpen_ConnectFails() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var socket = Create(lifetime);
            var instances = (Dictionary<int, JsWebSocket>)typeof(JsWebSocket).GetField("Instances", Flags).GetValue(null);
            instances[Id] = socket;

            try
            {
                Call(null, "OnClose", Id, 1006);

                try
                {
                    await (UniTask)Call(socket, "WaitOpened");
                    Assert.Fail("Connect succeeded over a socket that never opened");
                }
                catch (WebSocketClosedException)
                {
                }
            }
            finally
            {
                instances.Remove(Id);
                lifetime.Terminate();
            }
        });

        private static JsWebSocket Create(IReadOnlyLifetime lifetime)
        {
            var socket = (JsWebSocket)FormatterServices.GetUninitializedObject(typeof(JsWebSocket));

            Set(socket, "_lifetime", lifetime);
            Set(socket, "_handlerId", Id);
            var received = new ViewableDelegate<byte[]>();
            Set(socket, "_received", received);
            Set(socket, "_closed", new ViewableDelegate<SocketCloseInfo>());
            Set(socket, "_inbox", new SocketInbox<byte[]>(lifetime, bytes => received.Invoke(bytes)));

            return socket;
        }

        private static void Set(object target, string field, object value)
        {
            typeof(JsWebSocket).GetField(field, Flags).SetValue(target, value);
        }

        private static object Call(object target, string method, params object[] arguments)
        {
            return typeof(JsWebSocket).GetMethod(method, Flags).Invoke(target, arguments);
        }
    }
}
