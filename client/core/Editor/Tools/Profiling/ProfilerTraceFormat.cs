namespace Internal
{
    internal static class ProfilerTraceFormat
    {
        public static string Bytes(long bytes)
        {
            if (bytes < 1024L)
                return $"{bytes}B";

            if (bytes < 1024L * 1024L)
                return $"{bytes / 1024d:F1}KB";

            return $"{bytes / (1024d * 1024d):F1}MB";
        }

        public static string Duration(double ms)
        {
            if (ms < 1d)
                return $"{ms * 1000d:F0}µs";

            if (ms < 1000d)
                return $"{ms:F2}ms";

            return $"{ms / 1000d:F2}s";
        }
    }
}
