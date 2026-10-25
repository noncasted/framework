namespace Internal;

// The Task waits over the reactive primitives of Framework.Common (the Unity client has the UniTask ones).
public static class ReactiveTaskExtensions
{
    // Completes on the first invoke, is cancelled when the lifetime ends first.
    public static Task WaitInvoke(this IEventSource source, IReadOnlyLifetime lifetime)
    {
        var completion = new TaskCompletionSource();

        lifetime.Listen(() => completion.TrySetCanceled());
        source.Advise(lifetime, () => completion.TrySetResult());

        return completion.Task;
    }

    public static Task<T> WaitInvoke<T>(this IEventSource<T> source, IReadOnlyLifetime lifetime)
    {
        var completion = new TaskCompletionSource<T>();

        lifetime.Listen(() => completion.TrySetCanceled());
        source.Advise(lifetime, value => completion.TrySetResult(value));

        return completion.Task;
    }

    public static Task WaitFalse(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime)
    {
        return property.Wait(lifetime, false);
    }

    public static Task WaitTrue(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime)
    {
        return property.Wait(lifetime, true);
    }

    private static Task Wait(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime, bool expected)
    {
        if (property.Value == expected)
            return Task.CompletedTask;

        var completion = new TaskCompletionSource();
        lifetime.Listen(() => completion.TrySetCanceled());

        // The subscription lives on with the lifetime: a later change is not of this wait, the completed source
        // ignores it.
        property.Advise(lifetime, (_, value) => {
            if (value == expected)
                completion.TrySetResult();
        });

        return completion.Task;
    }
}
