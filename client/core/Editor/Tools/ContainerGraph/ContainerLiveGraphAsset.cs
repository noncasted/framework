using System;
using System.IO;
using Unity.GraphToolkit.Editor;

namespace Internal
{
    // The transient asset file of the graph: loaded when it exists, created otherwise.
    internal static class ContainerLiveGraphAsset
    {
        private static readonly ILog _log = Logs.Of(typeof(ContainerLiveGraphAsset));

        public static ContainerLiveGraph Ensure()
        {
            var directory = CatalogPaths.ToFullPath(Path.GetDirectoryName(ContainerLiveGraph.AssetPath));

            Directory.CreateDirectory(directory);

            try
            {
                if (File.Exists(Path.Combine(directory, "ContainerLiveGraph.containergraph")))
                {
                    var loaded = GraphDatabase.LoadGraph<ContainerLiveGraph>(ContainerLiveGraph.AssetPath);

                    if (loaded != null)
                        return loaded;
                }
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
            }

            return GraphDatabase.CreateGraph<ContainerLiveGraph>(ContainerLiveGraph.AssetPath);
        }
    }
}
