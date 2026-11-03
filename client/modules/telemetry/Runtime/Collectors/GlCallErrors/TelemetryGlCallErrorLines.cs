using System;
using System.Collections.Generic;
using System.Globalization;

namespace Global.Telemetry
{
    // The lines of TelemetryGlCallErrorsReadDetails: source, code, call, arguments, frame, time of the page, tab separated.
    public static class TelemetryGlCallErrorLines
    {
        public const string Checked = "check";
        public const string Recorded = "emscripten";

        public static void Parse(string text, List<TelemetryGlCallError> into)
        {
            foreach (var line in text.Split('\n'))
            {
                var fields = line.Split('\t');

                if (fields.Length != 6)
                    throw new FormatException($"[Telemetry] A line of an error of a WebGL call: '{line}'");

                into.Add(new TelemetryGlCallError(
                    IsCheckedSource(fields[0]),
                    int.Parse(fields[1], CultureInfo.InvariantCulture),
                    fields[2],
                    fields[3],
                    int.Parse(fields[4], CultureInfo.InvariantCulture),
                    double.Parse(fields[5], CultureInfo.InvariantCulture)));
            }
        }

        private static bool IsCheckedSource(string source)
        {
            return source switch
            {
                Checked => true,
                Recorded => false,
                _ => throw new FormatException($"[Telemetry] A source of an error of a WebGL call: '{source}'")
            };
        }
    }
}
