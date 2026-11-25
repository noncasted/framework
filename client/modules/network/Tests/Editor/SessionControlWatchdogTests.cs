using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Game.Session;
using NUnit.Framework;
using Shared;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The control connection of a P2P match pings the server and is dropped when the server is silent for
    // ConnectionSilence.Limit: a network lost without a close ends the match for the player. Real time:
    // the silence is counted in the time of the frames, so the run waits for it.
    [NoAutoStaticsCleanup]
    public class SessionControlWatchdogTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [UnityTest]
        public IEnumerator ServerSilent_ControlIsClosed() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (control, server) = await Start(lifetime);
                var isClosed = false;
                control.Closed.Advise(lifetime, _ => isClosed = true);

                server.Mute();

                await UniTask
                      .WaitUntil(() => isClosed == true)
                      .Timeout(TimeSpan.FromSeconds(ConnectionSilence.Limit + 5f), DelayType.Realtime);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // Idle, nothing to send but the pings: the answers keep the connection.
        [UnityTest]
        public IEnumerator ServerAnswers_ControlStaysOpen() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (control, _) = await Start(lifetime);
                var isClosed = false;
                control.Closed.Advise(lifetime, _ => isClosed = true);

                await UniTask.Delay(TimeSpan.FromSeconds(ConnectionSilence.Limit + 2f), DelayType.Realtime);

                Assert.IsFalse(isClosed);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private static async UniTask<(SessionControl Control, MutedServer Server)> Start(IReadOnlyLifetime lifetime)
        {
            var sessions = new LocalSessionServer(lifetime, "offline://control");
            var match = sessions.Create(SessionType.Match, 4, _ => Array.Empty<IService>());
            var server = new MutedServer(sessions);
            var servers = new LocalServers();
            servers.Add(lifetime, "control", server);

            var control = new SessionControl(new PlatformOptions(), new SilentNotice(), servers, new PeerSockets(),
                new NetworkProtocolHash("0123456789abcdef"), Logs.Of<SessionControl>());

            await control.Start(lifetime, "offline://control", sessions.GetTicket(match.Id, Guid.NewGuid(), 0)).Timeout(Timeout);
            return (control, server);
        }

        // The server of a network lost without a close: once muted, nothing it sends reaches the client.
        private class MutedServer : ILocalServer
        {
            public MutedServer(ILocalServer source)
            {
                _source = source;
            }

            private readonly ILocalServer _source;

            public bool IsMuted { get; private set; }

            public void Mute()
            {
                IsMuted = true;
            }

            public ILocalServerConnection Accept(IReadOnlyLifetime lifetime, ILocalClient client)
            {
                return _source.Accept(lifetime, new Client(client, this));
            }

            private class Client : ILocalClient
            {
                public Client(ILocalClient source, MutedServer server)
                {
                    _source = source;
                    _server = server;
                }

                private readonly ILocalClient _source;
                private readonly MutedServer _server;

                public void Send(byte[] bytes)
                {
                    if (_server.IsMuted == false)
                        _source.Send(bytes);
                }

                public void Close(SocketCloseInfo info)
                {
                    if (_server.IsMuted == false)
                        _source.Close(info);
                }
            }
        }

        private class SilentNotice : IClientOutdatedNotice
        {
            public bool IsShown => false;

            public void Show()
            {
            }
        }
    }
}
