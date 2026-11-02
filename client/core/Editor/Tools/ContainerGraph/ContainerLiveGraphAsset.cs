using System;
using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Internal
{
    // The transient asset file of the graph: loaded when it exists, created otherwise.
    internal static class ContainerLiveGraphAsset
    {
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
                Debug.LogException(exception);
            }

            return GraphDatabase.CreateGraph<ContainerLiveGraph>(ContainerLiveGraph.AssetPath);
        }
    }
}
