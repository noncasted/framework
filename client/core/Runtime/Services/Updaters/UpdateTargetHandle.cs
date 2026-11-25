using System;
using System.Collections.Generic;

namespace Internal
{
    // Equatable: the list removes a handle by value, and the default comparer of a struct without it boxed both
    // handles and compared their fields by reflection.
    public readonly struct UpdateTargetHandle<T> : IEquatable<UpdateTargetHandle<T>>
    {
        public UpdateTargetHandle(T target, IReadOnlyLifetime lifetime)
        {
            Target = target;
            Lifetime = lifetime;
        }

        public T Target { get; }
        public IReadOnlyLifetime Lifetime { get; }

        public bool Equals(UpdateTargetHandle<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Target, other.Target) == true
                   && ReferenceEquals(Lifetime, other.Lifetime) == true;
        }

        public override bool Equals(object obj)
        {
            return obj is UpdateTargetHandle<T> other && Equals(other) == true;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Target, Lifetime);
        }
    }
}
