using System.Collections.Generic;

namespace Internal
{
    // Tween lifetimes by target: a new tween of a target cuts its previous one.
    public sealed class TweenLifetimes<TKey>
    {
        private readonly Dictionary<TKey, ILifetime> _lifetimes = new();

        public IReadOnlyLifetime Restart(TKey key, IReadOnlyLifetime parent)
        {
            Kill(key);

            var lifetime = parent.Child();
            _lifetimes[key] = lifetime;

            return lifetime;
        }

        public void Kill(TKey key)
        {
            if (_lifetimes.Remove(key, out var lifetime))
                lifetime.Terminate();
        }

        public void KillAll()
        {
            foreach (var lifetime in _lifetimes.Values)
                lifetime.Terminate();

            _lifetimes.Clear();
        }

        // Forgets the tweens without cutting them: they play to the end by themselves.
        public void Clear()
        {
            _lifetimes.Clear();
        }
    }
}
