using System;
using UnityEngine.Events;

namespace Internal
{
    public static class UnityLifetimesIntegrationExtensions
    {
        public static IViewableDelegate<T> ToViewableDelegate<T>(
            this UnityEvent<T> source,
            IReadOnlyLifetime lifetime)
        {
            var viewableDelegate = new ViewableDelegate<T>();

            source.AddListener(Listener);
            lifetime.Listen(() => source.RemoveListener(Listener));

            return viewableDelegate;

            void Listener(T value)
            {
                viewableDelegate.Invoke(value);
            }
        }

        public static void Advise<T>(
            this UnityEvent<T> source,
            IReadOnlyLifetime lifetime,
            Action<T> action)
        {
            source.ToViewableDelegate(lifetime).Advise(lifetime, action);
        }

        public static void Listen<T>(this UnityEvent<T> source, IReadOnlyLifetime lifetime, UnityAction<T> listener)
        {
            source.AddListener(listener);

            lifetime.Listen(() => source.RemoveListener(listener));
        }

        public static void Listen<T>(this UnityEvent<T> source, IReadOnlyLifetime lifetime, UnityAction listener)
        {
            source.AddListener(Listener);

            lifetime.Listen(() => source.RemoveListener(Listener));

            void Listener(T value)
            {
                listener.Invoke();
            }
        }
    }
}