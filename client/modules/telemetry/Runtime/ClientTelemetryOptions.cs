using System;
using System.Collections.Generic;
using UnityEngine;

namespace Global.Telemetry
{
    // Built by the game in its scope.
    public class ClientTelemetryOptions
    {
        public ClientTelemetryOptions(
            float sendIntervalSeconds = 2f,
            float sampleIntervalSeconds = 1f,
            int bufferedSamples = 300,
            bool enabledInEditor = false,
            double startupFrameBudgetMs = 2d,
            float slowLoadSeconds = 5f,
            double slowShaderMs = 50d,
            IReadOnlyList<string> devProfilerMarkers = null)
        {
            if (sendIntervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(sendIntervalSeconds), sendIntervalSeconds, "[Telemetry] Must be above zero");

            if (sampleIntervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(sampleIntervalSeconds), sampleIntervalSeconds, "[Telemetry] Must be above zero");

            if (bufferedSamples <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferedSamples), bufferedSamples, "[Telemetry] Must be above zero");

            if (startupFrameBudgetMs < 0d)
                throw new ArgumentOutOfRangeException(nameof(startupFrameBudgetMs), startupFrameBudgetMs, "[Telemetry] Must not be negative");

            if (slowLoadSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(slowLoadSeconds), slowLoadSeconds, "[Telemetry] Must not be negative");

            if (slowShaderMs < 0d)
                throw new ArgumentOutOfRangeException(nameof(slowShaderMs), slowShaderMs, "[Telemetry] Must not be negative");

            devProfilerMarkers ??= Array.Empty<string>();

            foreach (var marker in devProfilerMarkers)
            {
                if (string.IsNullOrWhiteSpace(marker))
                    throw new ArgumentException("[Telemetry] A marker of the profiler without a name", nameof(devProfilerMarkers));
            }

            SendInterval = TimeSpan.FromSeconds(sendIntervalSeconds);
            SampleInterval = TimeSpan.FromSeconds(sampleIntervalSeconds);
            BufferedSamples = bufferedSamples;
            _enabledInEditor = enabledInEditor;
            StartupFrameBudgetMs = startupFrameBudgetMs;
            SlowLoadMs = slowLoadSeconds * 1000d;
            SlowShaderMs = slowShaderMs;
            DevProfilerMarkers = devProfilerMarkers;
        }

        private readonly bool _enabledInEditor;

        public TimeSpan SendInterval { get; }
        public TimeSpan SampleInterval { get; }

        // The samples kept while an answer of the server is late, at one sample per SampleInterval. Over it the oldest are lost.
        public int BufferedSamples { get; }

        // The time of a frame the preparation of a startup trace may take.
        public double StartupFrameBudgetMs { get; }

        // A load of an asset that runs longer gets the mark load.slow, once, at the next sample.
        public double SlowLoadMs { get; }

        // A single compilation or link of a shader of WebGL, or a query of its status, that runs longer: the mark webgl.shader.slow.
        public double SlowShaderMs { get; }

        // A development build only: the markers of the profiler timed per frame (dev.marker.<name>), by name: Unity's
        // ("Shader.CreateGPUProgram", "Camera.Render") or the ProfilerMarker of the game. None by default.
        public IReadOnlyList<string> DevProfilerMarkers { get; }

        // False: no session and no collecting in this run.
        public bool IsEnabled => Application.isEditor == false || _enabledInEditor;
    }
}
