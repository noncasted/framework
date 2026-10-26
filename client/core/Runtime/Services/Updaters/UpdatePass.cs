using System;
using System.Collections.Generic;

namespace Internal
{
    // One phase of an updater (Updater, UpdaterProxy) over its targets.
    internal static class UpdatePass
    {
        public static void Add<T>(IReadOnlyLifetime lifetime, T target, ModifiableList<UpdateTargetHandle<T>> list)
        {
            var handle = new UpdateTargetHandle<T>(target, lifetime);
            list.Add(handle);
            lifetime.Listen(() => list.Remove(handle));
        }

        // A target removed during the pass is still visited (the list defers the removal), so its lifetime is checked.
        // A target that throws does not stop the others: the exceptions are kept for Report after the phases.
        public static void Run<T>(
            ModifiableList<UpdateTargetHandle<T>> list,
            float delta,
            Action<T, float> update,
            ref List<Exception> exceptions)
        {
            foreach (var handle in list)
            {
                if (handle.Lifetime.IsTerminated == true)
                    continue;

                try
                {
                    update.Invoke(handle.Target, delta);
                }
                catch (Exception exception)
                {
                    (exceptions ??= new List<Exception>()).Add(exception);
                }
            }
        }

        public static void Report(List<Exception> exceptions)
        {
            if (exceptions != null)
                ReactiveExceptions.Report(exceptions);
        }
    }
}
