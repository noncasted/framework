using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Xunit;

namespace Framework.Tests.Messaging;

/// <summary>
/// A slow handler is not a dead observer: a timed-out request is not sent again (the handler may have run) and the
/// observer stays bound for the next requests.
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
        var lifetime = TestLifetime.Child();
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeout = OverrideConfig<IRuntimePipeConfig, RuntimePipeOptions>(new RuntimePipeOptions
        {
            SendTimeoutSeconds = 1
        });

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId, async request => {
            if (Interlocked.Increment(ref calls) == 1)
                await release.Task;

            return new TestResponse { Answer = request.Question };
        });

        var slow = () => messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "slow" });
        await slow.Should().ThrowAsync<TimeoutException>();

        // The slow call finishes in the observer, the next one waits behind it.
        release.SetResult();

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
        var lifetime = TestLifetime.Child();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The busy request outlasts the liveness check whatever the load, but must not time out itself.
        using var timeout = OverrideConfig<IRuntimePipeConfig, RuntimePipeOptions>(new RuntimePipeOptions
        {
            SendTimeoutSeconds = 30
        });

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId, async request => {
            if (request.Question == "busy")
            {
                entered.SetResult();
                await release.Task;
            }

            return new TestResponse { Answer = request.Question };
        });

        // The liveness ping waits behind the busy request of the observer.
        var busy = messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "busy" });
        await entered.Task;
        (await messaging.IsPipeExists(pipeId)).Should().BeFalse("the ping of a busy observer does not answer in time");
        release.SetResult();
        await busy;

        var response = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "next" });

        response.Answer.Should().Be("next");
        lifetime.Terminate();
    }
}
