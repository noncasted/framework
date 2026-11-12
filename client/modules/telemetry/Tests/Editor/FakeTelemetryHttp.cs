using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Global.Telemetry.Tests
{
    // Answers each POST at once by the script: a text, or a thrown TelemetryHttpException.
    public class FakeTelemetryHttp : ITelemetryHttp
    {
        public FakeTelemetryHttp(Func<string, string> answer)
        {
            _answer = answer;
        }

        private readonly Func<string, string> _answer;

        public List<(string Path, string Json)> Requests { get; } = new List<(string Path, string Json)>();

        public UniTask<string> Post(string path, string json)
        {
            Requests.Add((path, json));
            return UniTask.FromResult(_answer(path));
        }
    }
}
