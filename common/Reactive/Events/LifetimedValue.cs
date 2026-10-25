using System;

namespace Internal
{
    public class LifetimedValue<T> : ILifetimedValue<T>
    {
        // The value and its lifetime exist from the start, also for the default of a value type: Set skips an
        // equal value, so going through it here would leave ValueLifetime null for (false), (0) and the like.
        public LifetimedValue(T value)
        {
            _value = value;
            _lifetime = new Lifetime();
        }

        public LifetimedValue() : this(default)
        {
        }

        private readonly EventSource<IReadOnlyLifetime, T> _eventSource = new();

        private T _value;
        private Lifetime _lifetime;

        public T Value => _value;
        public IReadOnlyLifetime ValueLifetime => _lifetime;

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
        {
            _eventSource.Advise(lifetime, handler);
        }

        // An equal value is not a change: no notification, the value lifetime stays.
        public void Set(T value)
        {
            if (_value?.Equals(value) == true)
                return;

            _value = value;

            Notify();
        }

        // The value changed in place (a mutable one, a collection inside it): the value lifetime is renewed and the
        // listeners get the current value, as after Set.
        public void Notify()
        {
            _lifetime?.Terminate();
            _lifetime = new Lifetime();
            _eventSource.Invoke(_lifetime, _value);
        }

        // The listeners get the current value and its lifetime once more, nothing is renewed: for the ones already
        // advised (a late one catches up through View).
        public void InvokeAdvices()
        {
            _eventSource.Invoke(_lifetime, _value);
        }

        public void Dispose()
        {
            _lifetime?.Terminate();
            _eventSource.Dispose();
        }
    }
}
