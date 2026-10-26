using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Session;
using MemoryPack;
using NUnit.Framework;
using Shared;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The offline mode: the client stack (reader, writer) talks to a local session server through a loopback socket
    // as to the game gateway - the authorization by the ticket, LocalUpdate, ping, services and their properties,
    // refusals as ErrorResponse.
    public class LocalSessionServerTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        public IEnumerator Ticket_Authorized_LocalUpdateFollows() => UniTask.ToCoroutine(async () =>
        {
            using var client = new Client();
            var session = client.Server.Create(SessionType.Match, 1, _ => Array.Empty<IService>());

            var response = await client.Authorize(client.Server.GetTicket(session.Id));

            Assert.AreEqual(SessionAuthStatus.Ok, response.Status);

            var update = await client.WaitOneWay<SharedSessionPlayer.LocalUpdate>();
            Assert.AreEqual(LocalSession.FirstUserIndex, update.Index);
        });

        [UnityTest]
        public IEnumerator UnknownTicket_Refused_SocketClosed() => UniTask.ToCoroutine(async () =>
        {
            using var client = new Client();

            var response = await client.Authorize("offline.unknown");

            Assert.AreEqual(SessionAuthStatus.InvalidTicket, response.Status);
            await client.Closed.Task.Timeout(Timeout);
        });

        [UnityTest]
        public IEnumerator Ping_AnsweredByPong() => UniTask.ToCoroutine(async () =>
        {
            using var client = await Client.Joined();

            client.Writer.WriteOneWay(new SharedSessionLatency.Ping { Id = 7 });

            var pong = await client.WaitOneWay<SharedSessionLatency.Pong>();
            Assert.AreEqual(7, pong.Id);
        });

        [UnityTest]
        public IEnumerator EntityOutOfRange_RefusedWithForbidden() => UniTask.ToCoroutine(async () =>
        {
            using var client = await Client.Joined();

            var request = client.Writer.WriteRequest<SharedSessionEntity.CreateResponse>(new SharedSessionEntity.CreateRequest
            {
                Id = SharedEntityIds.Create(LocalSession.FirstUserIndex + 1, 1),
                Properties = Array.Empty<SharedSessionObject.PropertyUpdate>()
            });

            try
            {
                await request.Timeout(Timeout);
                Assert.Fail("The entity out of the range of the user was expected to be refused");
            }
            catch (NetworkRequestException exception)
            {
                Assert.AreEqual(ErrorCode.Forbidden, exception.Code);
            }
        });

        // A service of the session: the client gets its values with the response, its write reaches the service and is
        // not echoed, a write of the service itself is pushed.
        [UnityTest]
        public IEnumerator ServiceProperty_WrittenAndPushed() => UniTask.ToCoroutine(async () =>
        {
            var service = new CounterService();
            using var client = await Client.Joined(service);

            var get = await client.Writer
                                  .WriteRequest<SharedSessionService.GetResponse>(new SharedSessionService.GetRequest
                                  {
                                      Key = CounterService.ServiceKey,
                                      PropertiesIds = new[] { CounterService.PropertyId }
                                  })
                                  .Timeout(Timeout);

            Assert.AreEqual(Service.GetId(CounterService.ServiceKey), get.Id);
            Assert.AreEqual(1, get.Properties.Count);

            client.Writer.WriteOneWay(new SharedSessionObject.SetProperties
            {
                Entries = new[]
                {
                    new SharedSessionObject.SetProperty
                    {
                        ObjectId = get.Id,
                        PropertyId = CounterService.PropertyId,
                        Value = MemoryPackSerializer.Serialize(new SharedSessionLatency.Ping { Id = 3 })
                    }
                }
            });

            var update = await client.WaitOneWay<SharedSessionObject.PropertyUpdate>();
            var value = MemoryPackSerializer.Deserialize<SharedSessionLatency.Ping>(update.Value);

            // The service answered the write of 3 with 4; the write itself came back to nobody.
            Assert.AreEqual(4, value.Id);
            Assert.AreEqual(1, client.Count<SharedSessionObject.PropertyUpdate>());
        });

        private class CounterService : Service
        {
            public CounterService() : base(ServiceKey)
            {
                BindProperty(_counter);
            }

            public const string ServiceKey = "Counter";
            public const int PropertyId = 1;

            private readonly ValueProperty<SharedSessionLatency.Ping> _counter = new(PropertyId);

            protected override void OnStarted(IReadOnlyLifetime lifetime)
            {
                _counter.Written.Advise(lifetime, state => _counter.Set(new SharedSessionLatency.Ping { Id = state.Id + 1 }));
            }
        }

        private class Client : IDisposable
        {
            public Client()
            {
                var traffic = new NetworkTraffic();

                Server = new LocalSessionServer(_lifetime, "offline://game");
                Writer = new ConnectionWriter(traffic);
                _reader = new ConnectionReader(traffic);

                var socket = new LoopbackWebSocket("offline://game", Server, _lifetime);

                _reader.Run(_lifetime, socket);
                Writer.Run(_lifetime, socket);
                _reader.Response.Advise(_lifetime, response => Writer.OnRequestHandled(response.Context, response.RequestId));
                _reader.OneWay.Advise(_lifetime, message => _received.Add(message.Context));
                socket.Closed.Advise(_lifetime, info => Closed.TrySetResult(info));

                _connect = socket.Connect();
            }

            private readonly Lifetime _lifetime = new();
            private readonly ConnectionReader _reader;
            private readonly List<INetworkContext> _received = new();
            private readonly UniTask _connect;

            public readonly UniTaskCompletionSource<SocketCloseInfo> Closed = new();

            public LocalSessionServer Server { get; }
            public ConnectionWriter Writer { get; }

            public static async UniTask<Client> Joined(params IService[] services)
            {
                var client = new Client();
                var session = client.Server.Create(SessionType.Match, 1, _ => services);

                await client.Authorize(client.Server.GetTicket(session.Id));
                await client.WaitOneWay<SharedSessionPlayer.LocalUpdate>();

                return client;
            }

            public async UniTask<SharedSessionAuth.Response> Authorize(string ticket)
            {
                await _connect;

                return await Writer
                             .WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request { Ticket = ticket })
                             .Timeout(Timeout);
            }

            public async UniTask<T> WaitOneWay<T>() where T : class, INetworkContext
            {
                await UniTask.WaitUntil(() => _received.Exists(context => context is T)).Timeout(Timeout);
                return (T)_received.Find(context => context is T);
            }

            public int Count<T>()
            {
                return _received.FindAll(context => context is T).Count;
            }

            public void Dispose()
            {
                _lifetime.Terminate();
            }
        }
    }
}
