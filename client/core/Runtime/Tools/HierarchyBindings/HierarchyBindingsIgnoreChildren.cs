using UnityEngine;

namespace Internal
{
    // Cuts the mirror at this object: the object itself stays in the bindings together with its
    // components, but its children do not. Used where the object holds markup that does not belong
    // in code: scroll content, spawn roots and other containers.
    [DisallowMultipleComponent]
    public sealed class HierarchyBindingsIgnoreChildren : MonoBehaviour
    {
    }
}