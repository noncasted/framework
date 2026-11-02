using System;
using System.Collections.Generic;

namespace Internal
{
    // Every runtime registration must have a slot in the generated class: one the generator did not see
    // (a call through an interface or a virtual method, a stale asset map) would otherwise be dropped silently,
    // and the instances of a loop would all go into one slot.
    internal static class ContainerCoverage
    {
        public static void Verify(string rootId, List<ContainerEntry> entries, Type[] constructed, Type[] instances)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry.InstanceType == null)
                {
                    if (Array.IndexOf(constructed, entry.Implementation) < 0)
                    {
                        throw new InvalidOperationException(
                            $"Scope '{rootId}': {entry.Implementation.FullName} is registered at runtime, but the " +
                            "generated class does not know it. It was registered through code the generator does not " +
                            "follow (a call through an interface or a virtual method, reflection), or the asset map " +
                            "of the project is stale.");
                    }

                    continue;
                }

                var registered = 0;

                for (var j = 0; j < entries.Count; j++)
                {
                    if (entries[j].InstanceType == entry.InstanceType)
                        registered++;
                }

                var slots = 0;

                for (var j = 0; j < instances.Length; j++)
                {
                    if (instances[j] == entry.InstanceType)
                        slots++;
                }

                if (registered > slots)
                {
                    throw new InvalidOperationException(
                        $"Scope '{rootId}': {entry.InstanceType.FullName} is registered as an instance {registered} " +
                        $"time(s), the generated class has {slots} slot(s) for it. A registration in a loop, a repeated " +
                        "installer call, identical scene services or code the generator does not follow: one slot " +
                        "keeps one instance (builder.Inject takes every instance of a type).");
                }
            }
        }
    }
}
