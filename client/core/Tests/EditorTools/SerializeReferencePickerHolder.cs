using UnityEngine;

namespace Internal.EditorTests
{
    public sealed class SerializeReferencePickerHolder : ScriptableObject
    {
        public const string ValueField = nameof(_value);

        [SerializeReference] private SerializeReferencePickerTests.INested _value;
    }
}
