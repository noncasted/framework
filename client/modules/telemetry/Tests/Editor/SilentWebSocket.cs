using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry.Tests
{
    // A socket for the real writer: takes everything, never answers.
    public class SilentWebSocket : IWebSocket
    {
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new ViewableDelegate<SocketCloseInfo>();

        public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;

        public UniTask Connect() => UniTask.CompletedTask;
        public UniTask Send(ArraySegment<byte> bytes) => UniTask.CompletedTask;

        public void Drop(string reason)
        {
            _closed.Invoke(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, reason));
        }
    }
}
