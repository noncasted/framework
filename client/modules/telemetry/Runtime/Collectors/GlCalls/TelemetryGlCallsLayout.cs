namespace Global.Telemetry
{
    // The doubles of a take of ITelemetryGlCalls, the format between TelemetryGlCalls.jslib and the managed side: the counters, then
    // the slow calls as pairs of a kind and a duration.
    public static class TelemetryGlCallsLayout
    {
        public const int Compiles = 0;
        public const int CompileMs = 1;
        public const int Links = 2;
        public const int LinkMs = 3;
        public const int BufferBytes = 4;
        public const int BufferDataCalls = 5;
        public const int TextureBytes = 6;
        public const int TextureUploads = 7;
        public const int FirstSlow = 8;

        public const int MaxSlowPerTake = 8;
        public const int Length = FirstSlow + MaxSlowPerTake * 2;

        // The kinds of a slow call: compileShader, the queries of a shader (getShaderParameter, getShaderInfoLog), linkProgram, the
        // queries of a program. Chromium and ANGLE may defer a compilation or a link to the first query of its status.
        public const int SlowCompile = 0;
        public const int SlowCompileStatus = 1;
        public const int SlowLink = 2;
        public const int SlowLinkStatus = 3;
    }
}
