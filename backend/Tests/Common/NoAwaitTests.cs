using Common.Extensions;
using FluentAssertions;
using Xunit;

namespace Framework.Tests.Common;

// A task nobody awaits has no caller to throw to: its failure goes to NoAwaitExceptions.Handler when it happens.
[Collection(nameof(NoAwaitCollection))]
public class NoAwaitTests : IDisposable
{
    public NoAwaitTests()
    {
        _previous = NoAwaitExceptions.Handler;
        NoAwaitExceptions.Handler = e => _reported.Add(e);
    }

    private readonly Action<Exception>? _previous;
    private readonly List<Exception> _reported = [];

    public void Dispose()
    {
        NoAwaitExceptions.Handler = _previous;
    }

    [Fact]
    public void NoAwait_TaskFails_ReportedAtOnce()
    {
        var completion = new TaskCompletionSource();
        var error = new InvalidOperationException("loop died");

        completion.Task.NoAwait();
        completion.SetException(error);

        _reported.Should().ContainSingle().Which.Should().BeSameAs(error);
    }

    [Fact]
    public void NoAwait_TaskCancelled_NotReported()
    {
        var completion = new TaskCompletionSource();

        completion.Task.NoAwait();
        completion.SetCanceled(TestContext.Current.CancellationToken);

        _reported.Should().BeEmpty();
    }
}

// The handler is process-wide: the tests that replace it do not run in parallel with each other.
[CollectionDefinition(nameof(NoAwaitCollection), DisableParallelization = true)]
public class NoAwaitCollection;
