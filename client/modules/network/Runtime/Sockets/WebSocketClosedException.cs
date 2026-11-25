using System;

namespace Internal
{
    // The socket was closed while it was being opened: Connect fails with it.
    public class WebSocketClosedException : Exception
    {
        public WebSocketClosedException(int code) : base($"WebSocket closed while connecting: code={code}")
        {
            Code = code;
        }

        public int Code { get; }
    }
}
