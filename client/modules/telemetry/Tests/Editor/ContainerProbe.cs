using Internal;

namespace Global.Telemetry.Tests
{
    public static class ContainerProbe
    {
        public static RecordingTransport Transport { get; private set; }
        public static IUpdater Updater { get; private set; }
        public static ScriptedTelemetryWriter Writer { get; private set; }
        public static ITelemetrySocket Socket { get; private set; }

        public static void SetUpdater(IUpdater updater)
        {
            Updater = updater;
        }

        public static void Reset()
        {
            Transport = new RecordingTransport();
            Writer = new ScriptedTelemetryWriter();
            Socket = new FakeTelemetrySocket(Writer);
        }
    }
}
