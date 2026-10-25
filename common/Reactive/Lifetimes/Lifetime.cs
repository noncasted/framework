using System;
using System.Collections.Generic;
using System.Threading;

namespace Internal
{
    // Thread-safe: a lifetime may be terminated on one thread while another one listens or asks for the token
    // (network threads, thread pool continuations, grain callbacks). The state is guarded by a lock on this instance
    // (no extra object is allocated); listeners and the token callbacks are invoked outside of it.
    public class Lifetime : ILifetime
    {
        public Lifetime(IReadOnlyLifetime parent = null) : this(parent, parent, null)
        {
        }

        // The tracking parents are needed only by the Lifetime Graph of the editor and do not affect behaviour:
        // for Intersect termination is driven by subscriptions, while in the graph it hangs under both sources.
        private Lifetime(
            IReadOnlyLifetime parent,
            IReadOnlyLifetime trackingParent,
            IReadOnlyLifetime trackingSecondParent)
        {
            _parent = parent;

#if UNITY_EDITOR
            TrackingId = LifetimeTracker.OnCreated(trackingParent, trackingSecondParent);
#endif
        }

        private readonly IReadOnlyLifetime _parent;

        // Subscription to the parent: created once in Child(), removed in Terminate by the same instance.
        private Action _terminate;

        private List<Action> _listeners;
        private CancellationTokenSource _cancellation;
        private volatile bool _isTerminated;

        public CancellationToken Token
        {
            get
            {
                lock (this)
                {
                    // No source is created after termination: a cancelled token already exists.
                    if (_isTerminated == true && _cancellation == null)
                        return new CancellationToken(canceled: true);

                    _cancellation ??= new CancellationTokenSource();

                    // The source of a terminated lifetime is already cancelled (or is being cancelled right now by
                    // Terminate on another thread): its token reports it.
                    return _cancellation.Token;
                }
            }
        }

        public bool IsTerminated => _isTerminated;

#if UNITY_EDITOR
        // Node in the Lifetime Graph; 0 means it was created while tracking was off (LifetimeTracker).
        public int TrackingId { get; }
#endif

        internal Action TerminateCallback => _terminate ??= Terminate;

        internal static Lifetime CreateIntersection(IReadOnlyLifetime lifetimeA, IReadOnlyLifetime lifetimeB)
        {
            return new Lifetime(null, lifetimeA, lifetimeB);
        }

        public void Listen(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            lock (this)
            {
                if (_isTerminated == false)
                {
                    _listeners ??= new List<Action>();
                    _listeners.Add(callback);
                    return;
                }
            }

            // Already terminated, as a TerminatedLifetime: the callback runs at once. A subscription made by a
            // listener during Terminate ends up here too, the list has been taken by then.
            callback.Invoke();
        }

        public void RemoveListener(Action callback)
        {
            lock (this)
                _listeners?.Remove(callback);
        }

        public void Terminate()
        {
            List<Action> listeners;
            CancellationTokenSource cancellation;

            // The pass is one-shot: the whole list is taken so that Listen/RemoveListener
            // called from listeners (or from other threads) do not touch the collection being iterated.
            lock (this)
            {
                if (_isTerminated == true)
                    return;

                _isTerminated = true;
                listeners = _listeners;
                _listeners = null;
                cancellation = _cancellation;
            }

#if UNITY_EDITOR
            LifetimeTracker.OnTerminated(TrackingId);
#endif

            cancellation?.Cancel();

            // Listeners release resources, so every one of them runs; the failing ones are reported after the pass.
            List<Exception> exceptions = null;

            if (listeners != null)
            {
                foreach (var listener in listeners)
                {
                    try
                    {
                        listener.Invoke();
                    }
                    catch (Exception exception)
                    {
                        (exceptions ??= new List<Exception>()).Add(exception);
                    }
                }
            }

            // An intersection has no parent: its sources are unsubscribed by its own listener (Intersect).
            if (_terminate != null && _parent != null)
                _parent.RemoveListener(_terminate);

            if (exceptions != null)
                ReactiveExceptions.Report(exceptions);
        }
    }
}
