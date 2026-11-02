using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // One child on the way to its parent: scanned, but not named until it is known what collapses into what.
    internal sealed class HierarchyBindingsChildScan
    {
        public HierarchyBindingsChildScan(GameObject child, string childPath, string baseName)
        {
            Child = child;
            ChildPath = childPath;
            BaseName = baseName;
        }

        public GameObject Child { get; }
        public string ChildPath { get; }
        public string BaseName { get; }
        public string Signature { get; private set; }

        // The child has its own bindings: do not go inside, expose a reference to each of its bindings
        // components. Null - a regular child, then Node is set.
        public List<Component> Boundaries { get; private set; }
        public HierarchyBindingsNode Node { get; private set; }

        public void SetBoundaries(List<Component> boundaries, string signature)
        {
            Boundaries = boundaries;
            Signature = signature;
        }

        public void SetNode(HierarchyBindingsNode node, string signature)
        {
            Node = node;
            Signature = signature;
        }
    }
}
