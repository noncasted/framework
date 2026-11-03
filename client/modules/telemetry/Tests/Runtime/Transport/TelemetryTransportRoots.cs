using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry.Tests.Transport
{
    // The scope a game builds: its socket, the telemetry over it, the standard collectors, the real sources of the device and the trace.
    public static class TelemetryTransportRoots
    {
        public static ITelemetrySocket Socket { get; private set; }
        public static IUpdater Updater { get; private set; }
        public static string Tag { get; private set; }

        public static void Use(ITelemetrySocket socket, IUpdater updater, string tag)
        {
            Socket = socket;
            Updater = updater;
            Tag = tag;
        }

        [ContainerGraphRoot]
        public static UniTask Game(IScopeBuilder builder)
        {
            builder.RegisterInstance(Socket);
            builder.RegisterInstance(Updater);
            builder.AddSocketClientTelemetry(new ClientTelemetryOptions(0.1f, 0.05f, enabledInEditor: true));
            builder.AddStandardTelemetryCollectors();
            builder.Register<TelemetryTransportGameSession>().As<IScopeLoaded>();

            return UniTask.CompletedTask;
        }
    }
}
