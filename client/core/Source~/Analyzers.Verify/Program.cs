using System;

namespace Framework.Analyzers.Verify
{
    // Runs every case of the analyzers; exit code 1 on any failure.
    internal static class Program
    {
        public static int Main()
        {
            var harness = new Harness();
            PublicSetterCases.Run(harness);
            FileLengthCases.Run(harness);
            TopLevelTypesCases.Run(harness);
            CommentLanguageCases.Run(harness);
            LifetimeLockCases.Run(harness);
            return harness.Finish();
        }
    }
}
