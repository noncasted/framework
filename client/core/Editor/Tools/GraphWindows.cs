using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // A live graph is refreshed only while a window shows it.
    internal static class GraphWindows
    {
        public static bool IsDisplayed(Graph graph)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window is IGraphWindow graphWindow && ReferenceEquals(graphWindow.Graph, graph))
                    return true;
            }

            return false;
        }
    }
}
