using System;
using System.Collections;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.MatchMetrics.Tests
{
    // The failures of the network are null answers; a refusal or a silence of the server is said once per link, a lost
    // connection is not.
    public class MatchMetricsSocketTransportTests
    {
        private FailingMatchWriter _writer;
        private MatchMetricsSocketTransport _transport;

        [SetUp]
        public void SetUp()
        {
            _writer = new FailingMatchWriter();
            _transport = new MatchMetricsSocketTransport(new FakeMatchSocket(_writer), Logs.Of<MatchMetricsSocketTransport>());
        }

        private UniTask<SharedMatchMetrics.Opened> Open()
        {
            return _transport.Open(new SharedMatchMetrics.Open { MatchId = Guid.NewGuid(), WriterId = Guid.NewGuid() });
        }

        [UnityTest]
        public IEnumerator ServerError_NullAndOneWarningPerLink() => UniTask.ToCoroutine(async () =>
        {
            _writer.FailWith(new NetworkRequestException(ErrorCode.UnknownCommand, "no handler", "Open"));
            LogAssert.Expect(LogType.Warning, new Regex("UnknownCommand"));

            Assert.IsNull(await Open());
            Assert.IsNull(await Open());
            LogAssert.NoUnexpectedReceived();

            _writer.RunAgain();
            LogAssert.Expect(LogType.Warning, new Regex("UnknownCommand"));

            Assert.IsNull(await Open());
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator Timeout_NullAndAWarning() => UniTask.ToCoroutine(async () =>
        {
            _writer.FailWith(new TimeoutException("No response"));
            LogAssert.Expect(LogType.Warning, new Regex("No response"));

            Assert.IsNull(await Open());
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator Cancelled_NullAndSilent() => UniTask.ToCoroutine(async () =>
        {
            _writer.FailWith(new OperationCanceledException("The connection is closed"));

            Assert.IsNull(await Open());
            LogAssert.NoUnexpectedReceived();
        });
    }
}
