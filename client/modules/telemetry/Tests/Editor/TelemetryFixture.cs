using System;
using System.Collections.Generic;
using System.Linq;
using Internal;

namespace Global.Telemetry.Tests
{
    // The module put together by hand: a scripted collector, a manual clock, a transport that records the calls.
    public class TelemetryFixture
    {
        public TelemetryFixture(
            int bufferedSamples = 300,
            bool strictNames = true,
            bool enabledInEditor = true,
            TelemetryDevice device = null,
            float sendIntervalSeconds = 0.01f,
            float sampleIntervalSeconds = 0.01f,
            IReadOnlyList<ITelemetryCollector> collectors = null,
            IReadOnlyList<ITelemetryLiveCollector> liveCollectors = null,
            bool catchErrors = false,
            ITelemetryTransport transport = null,
            ITelemetryGlContext glContext = null,
            ITelemetryGlErrors glErrors = null,
            bool pageLifecycle = false,
            MemoryTraceStore traceStore = null)
        {
            TraceStore = traceStore ?? new MemoryTraceStore();
            Trace = new TelemetrySessionTrace(TraceStore, WallClock);
            Options = new ClientTelemetryOptions(
                sendIntervalSeconds,
                sampleIntervalSeconds,
                bufferedSamples,
                enabledInEditor,
                double.MaxValue);
            Guard = new TelemetryNameGuard(strictNames);
            Names = new TelemetryNameTable(Guard);
            Buffer = new TelemetryBuffer(Options, Trace);
            Writer = new TelemetryBatchWriter(Names, Buffer);
            Visibility = new TelemetryVisibility(Page);
            Errors = new TelemetryErrors(Buffer, Clock);
            ErrorQueue = new TelemetryErrorQueue();

            var glCollector = glContext == null ? null : new GlContextTelemetryCollector(glContext, Buffer, Clock, ErrorQueue);

            var glErrorsCollector = glErrors == null ? null : new GlErrorsTelemetryCollector(glErrors, Buffer, Clock, ErrorQueue);

            if (glCollector != null)
                collectors = new ITelemetryCollector[] { Collector, glCollector };

            if (glErrorsCollector != null)
                collectors = (collectors ?? new ITelemetryCollector[] { Collector }).Append(glErrorsCollector).ToArray();

            collectors = (collectors ?? new ITelemetryCollector[] { Collector }).Append(Added).ToArray();
            Sampler = new TelemetrySampler(collectors, Names, Buffer, Clock, Visibility);

            if (catchErrors)
            {
                liveCollectors = new ITelemetryLiveCollector[]
                {
                    new TelemetryLogErrors(ErrorQueue),
                    new TelemetryErrorPump(
                        ErrorQueue,
                        new TelemetryTextCleaner(),
                        new TelemetryErrorGroups(Errors, Clock),
                        Options)
                };
            }

            // As in AddClientTelemetry: after the pump, which opens the queue of the errors for the session.
            if (glCollector != null)
                liveCollectors = (liveCollectors ?? Array.Empty<ITelemetryLiveCollector>()).Append(glCollector).ToArray();

            if (glErrorsCollector != null)
                liveCollectors = (liveCollectors ?? Array.Empty<ITelemetryLiveCollector>()).Append(glErrorsCollector).ToArray();

            if (pageLifecycle)
                liveCollectors = (liveCollectors ?? Array.Empty<ITelemetryLiveCollector>()).Append(new PageLifecycleTelemetryCollector(Page, Buffer, Clock)).ToArray();

            liveCollectors = (liveCollectors ?? Array.Empty<ITelemetryLiveCollector>()).Append(Added).ToArray();

            Collection = new TelemetryCollection(
                Buffer,
                Names,
                Sampler,
                Writer,
                Clock,
                Visibility,
                Trace,
                liveCollectors ?? Array.Empty<ITelemetryLiveCollector>());

            var deviceSource = new FixedDeviceSource(device ?? new TelemetryDevice());

            Gate = new TelemetryRequestGate();
            Startup = new TelemetryStartupReport(Options, StartupSource, transport ?? Transport, Gate, deviceSource);

            Telemetry = new ClientTelemetry(
                Options,
                Guard,
                Buffer,
                Clock,
                Collection,
                Writer,
                transport ?? Transport,
                deviceSource,
                Gate,
                Startup,
                Added,
                Trace);
        }

        public ScriptedCollector Collector { get; } = new ScriptedCollector();
        public TelemetryAddedCollectors Added { get; } = new TelemetryAddedCollectors();
        public ManualClock Clock { get; } = new ManualClock();
        public RecordingTransport Transport { get; } = new RecordingTransport();
        public FakeTelemetryPage Page { get; } = new FakeTelemetryPage();
        public ClientTelemetryOptions Options { get; }
        public TelemetryNameGuard Guard { get; }
        public TelemetryNameTable Names { get; }
        public TelemetryBuffer Buffer { get; }
        public TelemetryBatchWriter Writer { get; }
        public TelemetryVisibility Visibility { get; }
        public TelemetrySampler Sampler { get; }
        public TelemetryErrors Errors { get; }
        public TelemetryErrorQueue ErrorQueue { get; }
        public TelemetryCollection Collection { get; }
        public ClientTelemetry Telemetry { get; }
        public FakeTelemetryStartupSource StartupSource { get; } = new FakeTelemetryStartupSource();
        public TelemetryRequestGate Gate { get; }
        public TelemetryStartupReport Startup { get; }
        public MemoryTraceStore TraceStore { get; }
        public ManualWallClock WallClock { get; } = new ManualWallClock();
        public TelemetrySessionTrace Trace { get; }

        public void Sample(long time, Action<ITelemetrySample> collect)
        {
            Clock.Set(time);
            Collector.Script(collect);
            Sampler.Sample();
        }

        public static IReadOnlyList<string> NamesOf(TelemetryBatch batch)
        {
            return batch.Names.Select(name => name.Name).ToArray();
        }
    }

    public class ScriptedCollector : ITelemetryCollector
    {
        private Action<ITelemetrySample> _collect = _ => { };

        public int Calls { get; private set; }

        public void Script(Action<ITelemetrySample> collect)
        {
            _collect = collect;
        }

        public void Collect(ITelemetrySample sample)
        {
            Calls++;
            _collect(sample);
        }
    }

    public class ManualClock : ITelemetryClock
    {
        public long ElapsedMilliseconds { get; private set; }
        public int Restarts { get; private set; }

        public void Set(long milliseconds)
        {
            ElapsedMilliseconds = milliseconds;
        }

        public void Restart()
        {
            Restarts++;
            ElapsedMilliseconds = 0;
        }
    }

    public class ManualWallClock : ITelemetryWallClock
    {
        public long NowMilliseconds { get; private set; }

        public void Set(long milliseconds)
        {
            NowMilliseconds = milliseconds;
        }
    }

    // The storage of the page: shared by two fixtures, it is a page that died and the next one.
    public class MemoryTraceStore : ITelemetryTraceStore
    {
        public string Text { get; private set; }

        public string Read()
        {
            return Text;
        }

        public void Write(string text)
        {
            Text = text;
        }

        public void Clear()
        {
            Text = null;
        }
    }

    public class FixedDeviceSource : ITelemetryDeviceSource
    {
        public FixedDeviceSource(TelemetryDevice device)
        {
            _device = device;
        }

        private readonly TelemetryDevice _device;

        public TelemetryDevice Read()
        {
            return _device;
        }
    }

    public class FakeTelemetryPage : ITelemetryPage
    {
        private readonly EventSource _becameHidden = new EventSource();
        private readonly EventSource<TelemetryPageEvent> _lifecycle = new EventSource<TelemetryPageEvent>();

        public bool IsHidden { get; private set; }
        public IEventSource BecameHidden => _becameHidden;
        public IEventSource<TelemetryPageEvent> Lifecycle => _lifecycle;

        public void Fire(TelemetryPageEvent kind)
        {
            _lifecycle.Invoke(kind);
        }

        public void Hide()
        {
            IsHidden = true;
            _becameHidden.Invoke();
        }

        public void Show()
        {
            IsHidden = false;
        }
    }
}
