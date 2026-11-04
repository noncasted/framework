using Internal;

namespace Global.FrameRate
{
    // The automatic frame rate: the high rate while the frames are cheap, the low one while they are not (AutoFrameRateRule).
    public interface IAutoFrameRate
    {
        // The limit it picks: the one remembered on the device as soon as it runs, then its changes.
        IViewableProperty<int> Target { get; }

        // A change of the limit made by the rule (not the start): the new limit.
        IEventSource<int> Switched { get; }

        // Measures the frames and decides while the lifetime lives (FrameRateLimit runs it for the Auto of the settings).
        void Run(IReadOnlyLifetime lifetime);
    }
}
