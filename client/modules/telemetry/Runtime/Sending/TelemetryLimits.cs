namespace Global.Telemetry
{
    // The limits of the contract: backend/ClientTelemetry/Contract/ClientTelemetryLimits.cs. The server rejects a whole
    // batch over any of them, so the client never writes one.
    public static class TelemetryLimits
    {
        public const int MaxBatchBytes = 64 * 1024;
        public const int MaxNamesPerSession = 200;
        public const int MaxTagsPerSession = 8;
        public const int MaxSamplesPerBatch = 300;
        public const int MaxErrorsPerBatch = 32;
        public const int MaxMarksPerBatch = 128;
        public const int MaxMessageLength = 2 * 1024;
        public const int MaxStackLength = 8 * 1024;
        public const int MaxCategoryLength = 256;
        public const int MaxNameLength = 64;
        public const int MaxUnitLength = 16;

        // The server cuts the tail of a startup trace over the first and the last one, the size estimated by StartupSpanBytes
        // and the length of the name of each span.
        public const int MaxStartupSpans = 4096;
        public const int MaxStartupNameLength = 128;
        public const int MaxStartupBytes = 256 * 1024;
        public const int StartupSpanBytes = 42;
        public const int MaxStartupFileLength = 64;
        public const int MaxMarkDetailLength = 128;

        public static string Cut(string text, int length)
        {
            return text.Length <= length ? text : text.Substring(0, length);
        }
    }
}
