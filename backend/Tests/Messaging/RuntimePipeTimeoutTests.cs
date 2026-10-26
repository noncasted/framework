using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Xunit;

namespace Framework.Tests.Messaging;

/// <summary>
/// A slow handler is not a dead observer: a timed-out request is not sent again (the handler may have run) and the
/// observer stays bound for the next requests. The fixture sets SendTimeoutSeconds to 2.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimePipeTimeoutTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Send_HandlerTimesOut_NotRetriedAndObserverKept()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();
        var calls = 0;

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId, async request => {
            if (Interlocked.Increment(ref calls) == 1)
                await Task.Delay(TimeSpan.FromSeconds(3));

            return new TestResponse { Answer = request.Question };
        });

        var slow = () => messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "slow" });
        await slow.Should().ThrowAsync<TimeoutException>();

        // The slow call finishes in the observer.
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);

        var response = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "next" });

        response.Answer.Should().Be("next");
        calls.Should().Be(2, "every request runs the handler once");
        lifetime.Terminate();
    }

    [Fact]
    public async Task Exists_ObserverBusy_ObserverKept()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId, async request => {
            if (request.Question == "busy")
                await Task.Delay(TimeSpan.FromSeconds(1.5));

            return new TestResponse { Answer = request.Question };
        });

        // The liveness ping waits behind the busy request of the observer.
        var busy = messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "busy" });
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await messaging.IsPipeExists(pipeId);
        await busy;

        var response = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "next" });

        response.Answer.Should().Be("next");
        lifetime.Terminate();
    }
}
