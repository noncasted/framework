namespace Global.Telemetry.Tests
{
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
}
