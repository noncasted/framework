using System;

namespace Internal
{
    // Builds the generated class of a root from what the installer wrote into the builder.
    internal static class GeneratedScopeCreator
    {
        public static IContainer Create(string rootId, ContainerBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (GeneratedScopeRegistry.TryGet(rootId, out var factory) == false)
            {
                throw new InvalidOperationException(
                    $"No generated container for '{rootId}'. Check generator diagnostics (CINGR00*) for this root.");
            }

            builder.VerifyCovered(rootId, factory.Constructed, factory.Instances);

            var lifetime = builder.CreateLifetime();
            IContainer created = null;

            try
            {
                var request = new GeneratedScopeRequest(rootId, builder, builder.Parent, lifetime);
                var allocatedBefore = ContainerDiagnostics.AllocatedBytes();
                created = factory.Create.Invoke(request);
                var buildBytes = ContainerDiagnostics.AllocatedBytes() - allocatedBefore;

                if (created == null)
                {
                    throw new InvalidOperationException(
                        $"Generated factory for '{rootId}' returned null.");
                }

                builder.MarkBuilt();
                AttachDebug(created, builder, buildBytes);
                return created;
            }
            catch
            {
                if (created != null)
                    created.Dispose();
                else
                    lifetime.Terminate();

                throw;
            }
        }

        private static void AttachDebug(IContainer created, ContainerBuilder builder, long buildBytes)
        {
#if UNITY_EDITOR || DEBUG
            // LoadAssetGroup records by the scope builder's lifetime, not by the container's child lifetime.
            // Take them even without diagnostics, otherwise records would pile up while it is off.
            var loadedAssets = ContainerRegistryDebug.TakeLoadedAssets(builder.ScopeLifetime);
            var diagnostics = created.Diagnostics;

            if (diagnostics == null)
                return;

            if (diagnostics is ContainerDiagnostics own)
            {
                own.SetLoadedAssets(loadedAssets);
                own.RecordAllocation(null, null, buildBytes, false);
            }

            ContainerRegistryDebug.Add(diagnostics);

            if (created.Lifetime != null)
                created.Lifetime.Listen(() => ContainerRegistryDebug.Remove(diagnostics));
#endif
        }
    }
}
