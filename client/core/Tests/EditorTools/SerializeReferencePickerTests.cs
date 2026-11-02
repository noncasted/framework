using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    public sealed class SerializeReferencePickerHolder : ScriptableObject
    {
        public const string ValueField = nameof(_value);

        [SerializeReference] private SerializeReferencePickerTests.INested _value;
    }

    public class SerializeReferencePickerTests
    {
        public interface INested
        {
        }

        [Serializable]
        public sealed class NestedImplementation : INested
        {
        }

        // Unity names a nested type Outer/Inner in managedReferenceFieldTypename.
        [Test]
        public void NestedFieldType_IsResolved()
        {
            var holder = ScriptableObject.CreateInstance<SerializeReferencePickerHolder>();

            try
            {
                var property = new SerializedObject(holder).FindProperty(SerializeReferencePickerHolder.ValueField);

                Assert.That(SerializeReferencePickerDrawer.ResolveFieldType(property.managedReferenceFieldTypename),
                    Is.EqualTo(typeof(INested)));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
