using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The UniTask waits over the reactive primitives of Framework.Common (the backend has the Task ones).
    public static class ReactiveUniTaskExtensions
    {
        // Completes on the first invoke of the subscribed listener, is cancelled when the lifetime ends first.
        // Both subscriptions are removed in either case.
        public static async UniTask WaitInvoke(
            this IReadOnlyLifetime lifetime,
            Action<Action> subscribe,
            Action<Action> unsubscribe)
        {
            var completion = new UniTaskCompletionSource();

            subscribe(Listener);
            lifetime.Listen(OnTerminated);

            try
            {
                await completion.Task;
            }
            finally
            {
                unsubscribe(Listener);
                lifetime.RemoveListener(OnTerminated);
            }

            void Listener()
            {
                completion.TrySetResult();
            }

            void OnTerminated()
            {
                completion.TrySetCanceled();
            }
        }

        // Completes in the Terminate of the lifetime, at once for a terminated one (Listen runs the callback then).
        // Unlike UniTask.WaitUntilCanceled(lifetime.Token) it does not wait for the next player loop.
        public static UniTask WaitTermination(this IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource();
            lifetime.Listen(() => completion.TrySetResult());
            return completion.Task;
        }

        public static UniTask WaitInvoke(this IEventSource viewableDelegate, IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource();

            lifetime.Listen(() => {
                completion.TrySetException(new OperationCanceledException());
            });

            viewableDelegate.Advise(lifetime, () => {
                completion.TrySetResult();
            });

            return completion.Task;
        }

        public static UniTask<T> WaitInvoke<T>(
            this IEventSource<T> viewableDelegate,
            IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource<T>();

            lifetime.Listen(() => completion.TrySetException(new OperationCanceledException()));

            viewableDelegate.Advise(lifetime, value => {
                completion.TrySetResult(value);
            });

            return completion.Task;
        }

        public static UniTask WaitFalse(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime)
        {
            return property.Wait(lifetime, false);
        }

        public static UniTask WaitTrue(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime)
        {
            return property.Wait(lifetime, true);
        }

        private static UniTask Wait(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime, bool expected)
        {
            if (property.Value == expected)
                return UniTask.CompletedTask;

            var completion = new UniTaskCompletionSource();
            lifetime.Listen(() => completion.TrySetCanceled());

            // The subscription lives on with the lifetime: a later change is not of this wait (the level is not ready
            // again between two floors), the completed source ignores it.
            property.Advise(lifetime, (_, value) => {
                if (value == expected)
                    completion.TrySetResult();
            });

            return completion.Task;
        }
    }
}
