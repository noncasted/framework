namespace Global.Telemetry
{
    // The first error of a call: Call is the method of the context (bindTexture) under the check, the gl* function of emscripten
    // (glGetIntegerv) otherwise; Arguments and Frame (the frames of the page since the start of the check) of the check only.
    public readonly struct TelemetryGlCallError
    {
        public TelemetryGlCallError(bool isChecked, int code, string call, string arguments, int frame, double pageMs)
        {
            IsChecked = isChecked;
            Code = code;
            Call = call;
            Arguments = arguments;
            Frame = frame;
            PageMs = pageMs;
        }

        public bool IsChecked { get; }
        public int Code { get; }
        public string Call { get; }
        public string Arguments { get; }
        public int Frame { get; }
        public double PageMs { get; }
    }
}
