using System;
using Internal;

namespace Internal
{
    public static class StateMachineExtensions
    {
        public static void View<T>(
            this IViewableProperty<T> property,
            IStateHandle stateHandle,
            Action listener) => property.View(stateHandle.Lifetime, listener);

        public static void View<T>(
            this IViewableProperty<T> property,
            IStateHandle stateHandle,
            Action<T> listener) => property.View(stateHandle.Lifetime, listener);

        public static void View<T>(
            this IViewableProperty<T> property,
            IStateHandle stateHandle,
            Action<IReadOnlyLifetime, T> listener) => property.View(stateHandle.Lifetime, listener);

        /// <summary>
        /// Exits the state when <paramref name="lifetime"/> terminates, for example when an external object
        /// the state works with is destroyed. The subscription lives while the state is active.
        /// </summary>
        public static void ExitOnTermination(this IStateHandle stateHandle, IReadOnlyLifetime lifetime)
        {
            Action exit = stateHandle.Exit;
            lifetime.Listen(exit);
            stateHandle.Lifetime.Listen(() => lifetime.RemoveListener(exit));
        }
    }
}
