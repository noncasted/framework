using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Backend.Tests
{
    // The HTTP requests of the backend module against a local HttpListener: a silent server fails the request in
    // BackendOptions.TimeoutSeconds; the response of a POST reaches the caller; the headers of an image request are
    // sent.
    public class BackendRequestsTests
    {
        [UnityTest]
        public IEnumerator Get_SilentServer_FailsInOptionsTimeout() => UniTask.ToCoroutine(async () =>
        {
            using var server = new Server(_ => Thread.Sleep(TimeSpan.FromSeconds(10)));
            var options = JsonUtility.FromJson<BackendOptions>("{\"_timeoutSeconds\":1}");
            var get = new BackendGet(options);
            var lifetime = new Lifetime();

            Exception exception = null;

            try
            {
                await get.Get(lifetime, new GetRequest(server.Url, Array.Empty<IRequestHeader>()))
                         .Timeout(TimeSpan.FromSeconds(5));
            }
            catch (Exception caught)
            {
                exception = caught;
            }
            finally
            {
                lifetime.Terminate();
            }

            Assert.IsNotNull(exception);
            Assert.IsNotInstanceOf<TimeoutException>(exception, "the request waited past the timeout of the options");
        });

        [UnityTest]
        public IEnumerator Post_ReturnsResponseBody() => UniTask.ToCoroutine(async () =>
        {
            using var server = new Server(context => Respond(context, Encoding.UTF8.GetBytes("pong")));
            var post = new BackendPost(new BackendOptions());
            var lifetime = new Lifetime();

            try
            {
                var response = await post.Post(lifetime, new PostRequest(server.Url, "ping", new[] { RequestHeader.Json() }));

                Assert.AreEqual("pong", response);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        [UnityTest]
        public IEnumerator GetImage_SendsHeaders() => UniTask.ToCoroutine(async () =>
        {
            var texture = new Texture2D(2, 2);
            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            using var server = new Server(context => {
                if (context.Request.Headers["X-Test"] != "1")
                {
                    context.Response.StatusCode = 401;
                    context.Response.Close();
                    return;
                }

                Respond(context, png);
            });

            var media = new BackendMedia(new BackendOptions());
            var lifetime = new Lifetime();

            try
            {
                var image = await media.GetImage(lifetime, new GetRequest(server.Url, new IRequestHeader[] { new RequestHeader("X-Test", "1") }));

                Assert.AreEqual(2, image.width);
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private static void Respond(HttpListenerContext context, byte[] body)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = body.Length;
            context.Response.OutputStream.Write(body, 0, body.Length);
            context.Response.Close();
        }

        private class Server : IDisposable
        {
            public Server(Action<HttpListenerContext> handler)
            {
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                var port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();

                Url = $"http://127.0.0.1:{port}/";
                _listener.Prefixes.Add(Url);
                _listener.Start();

                Task.Run(() => {
                    try
                    {
                        while (true)
                        {
                            var context = _listener.GetContext();
                            Task.Run(() => handler(context));
                        }
                    }
                    catch (Exception)
                    {
                        // Stopped by Dispose.
                    }
                });
            }

            private readonly HttpListener _listener = new();

            public string Url { get; }

            public void Dispose()
            {
                _listener.Stop();
            }
        }
    }
}
