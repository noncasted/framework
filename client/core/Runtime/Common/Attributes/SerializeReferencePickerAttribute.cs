using System;
using UnityEngine;

namespace Internal
{
    // Type dropdown for a [SerializeReference] field: the Unity inspector does not draw one by itself.
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class SerializeReferencePickerAttribute : PropertyAttribute
    {
    }
}
