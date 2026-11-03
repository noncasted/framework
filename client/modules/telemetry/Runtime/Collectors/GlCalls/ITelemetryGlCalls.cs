namespace Global.Telemetry
{
    // The calls of the WebGL context of the game counted on the page (TelemetryGlCalls.jslib): compilations and links of the shaders
    // with their time, uploads of buffers and textures. What happened since the previous take.
    public interface ITelemetryGlCalls
    {
        // Fills the array by TelemetryGlCallsLayout, at least TelemetryGlCallsLayout.Length long. The number of the slow calls written.
        int Take(double[] into);
    }
}
