namespace Internal
{
    // The channel the two peers negotiate through (the session socket of the game, a test pair). The signals of one
    // side reach the other in their order. Received is invoked on the main thread.
    public interface IWebRtcSignaling
    {
        IViewableDelegate<WebRtcSignal> Received { get; }

        void Send(WebRtcSignal signal);
    }
}
