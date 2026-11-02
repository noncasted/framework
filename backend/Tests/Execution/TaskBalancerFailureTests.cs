using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Common;
using Framework.Tests.Fixtures;
using Infrastructure.Execution;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Execution;

/// <summary>
/// The failure paths of the TaskBalancer: the penalty of a failing task and the drain at the end of the lifetime.
/// </summary>
[Collection(nameof(NoAwaitCollection))]
public class TaskBalancerFailureTests : IDisposable
{
    public TaskBalancerFailureTests()
    {
        _previous = NoAwaitExceptions.Handler;
        NoAwaitExceptions.SetHandler(e => {
            lock (_reported)
                _reported.Add(e);
        });
    }

    private readonly Action<Exception>? _previous;
    private readonly List<Exception> _reported = [];

    public void Dispose()
    {
        NoAwaitExceptions.SetHandler(_previous);
    }

    [Fact]
    public async Task FailingCriticalTask_PenaltyLetsLowTasksRun()
    {
        var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);
        var balancer = CreateBalancer(queue, concurrentTasks: 1);

        var criticalRuns = 0;
        var lowDone = 0;

        queue.Enqueue(new LambdaTask("critical", TaskPriority.Critical, () => {
            Interlocked.Increment(ref criticalRuns);
            throw new InvalidOperationException("always fails");
        }));

        // A backlog of low tasks, 50 ms each: about 0.5 s of work, five collect ticks of the balancer.
        for (var i = 0; i < 10; i++)
        {
            queue.Enqueue(new LambdaTask($"low-{i}", TaskPriority.Low, () => {
                Thread.Sleep(50);
                Interlocked.Increment(ref lowDone);
            }));
        }

        var lifetime = new Lifetime();
        await balancer.Run(lifetime);
        await TestWait.Until(() => Volatile.Read(ref lowDone) == 10);
        var runs = Volatile.Read(ref criticalRuns);
        lifetime.Terminate();

        runs.Should().BeLessThanOrEqualTo(2,
            "after a failure the penalty (50) puts the critical task (40) below the low ones (10)");
    }

    [Fact]
    public async Task LifetimeEnds_DrainCompletesWithoutError()
    {
        var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);
        var balancer = CreateBalancer(queue, concurrentTasks: 4);
        var finished = 0;

        for (var i = 0; i < 4; i++)
        {
            queue.Enqueue(new LambdaTask($"task-{i}", TaskPriority.Medium, () => {
                Thread.Sleep(100);
                Interlocked.Increment(ref finished);
            }));
        }

        var lifetime = new Lifetime();
        await balancer.Run(lifetime);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        lifetime.Terminate();
        await TestWait.Until(() => Volatile.Read(ref finished) == 4);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        lock (_reported)
            _reported.Should().BeEmpty("the drain of the execute loop must not fail");
    }

    private static TaskBalancer CreateBalancer(ITaskQueue queue, int concurrentTasks)
    {
        var config = Substitute.For<ITaskBalancerConfig>();

        config.Value.Returns(new TaskBalancerOptions
        {
            EmptyDelayMs = 10,
            NextDelayMs = 100,
            IterationScore = 1,
            ExceptionPenalty = 50,
            ConcurrentTasks = concurrentTasks
        });

        return new TaskBalancer(queue, NullLogger<TaskBalancer>.Instance, config);
    }

    private class LambdaTask(string id, TaskPriority priority, Action action) : IPriorityTask
    {
        public string Id => id;
        public TaskPriority Priority => priority;
        public TimeSpan Delay => TimeSpan.Zero;

        public Task Execute()
        {
            action();
            return Task.CompletedTask;
        }
    }
}
