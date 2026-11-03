namespace Global.Telemetry
{
    // The context of the game as numbers, read once per session. Below zero is not known (a lost context, an old browser).
    public interface ITelemetryWebGlInfo
    {
        // 1 or 2.
        int Version { get; }
        int MaxTextureSize { get; }

        // 1 or 0: the attributes the context got, not the ones asked for.
        int Antialias { get; }
        int PreserveDrawingBuffer { get; }

        // TelemetryPowerPreference.
        int PowerPreference { get; }

        // 1 or 0: EXT_disjoint_timer_query_webgl2, the source of frame.gpu.query.
        int TimerQuery { get; }
    }
}
