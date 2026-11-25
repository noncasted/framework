using System;
using UnityEngine;

namespace Internal
{
    // A dropdown list for a string field. The options come from a parameterless method of the same object returning
    // IEnumerable<DropdownOption>. VisibleIf is the name of a bool property or bool field of the object: while it is
    // false, the field is hidden in the inspector.
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class DropdownAttribute : PropertyAttribute
    {
        public DropdownAttribute(string optionsMethod)
        {
            OptionsMethod = optionsMethod;
        }

        public string OptionsMethod { get; }
        public string VisibleIf { get; set; }
    }
}
