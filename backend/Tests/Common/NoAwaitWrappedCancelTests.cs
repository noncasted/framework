using Common.Extensions;
using FluentAssertions;
using Xunit;

namespace Framework.Tests.Common;

// Only a cancelled task (or a failure that is all OperationCanceledException) is a quiet end. A failure of another type
// that merely wraps a cancellation (a transport error caused by an aborted read) is a real failure and is reported.
[Collection(nameof(NoAwaitCollection))]
public class NoAwaitWrappedCancelTests : IDisposable
{
    public NoAwaitWrappedCancelTests()
    {
        _previous = NoAwaitExceptions.Handler;
        NoAwaitExceptions.SetHandler(e => _reported.Add(e));
    }

    private readonly Action<Exception>? _previous;
    private readonly List<Exception> _reported = [];

    public void Dispose()
    {
        NoAwaitExceptions.SetHandler(_previous);
    }

    [Fact]
    public void NoAwait_FailureWrapsCancellation_Reported()
    {
        var completion = new TaskCompletionSource();
        var error = new InvalidOperationException("loop died", new OperationCanceledException());

        completion.Task.NoAwait();
        completion.SetException(error);

        _reported.Should().ContainSingle().Which.Should().BeSameAs(error);
    }

    [Fact]
    public void NoAwait_FailsWithCancellation_NotReported()
    {
        var completion = new TaskCompletionSource();

        completion.Task.NoAwait();
        completion.SetException(new OperationCanceledException());

        _reported.Should().BeEmpty();
    }
}
