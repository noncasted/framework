using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Internal
{
    [CreateAssetMenu(fileName = "ColorCatalog", menuName = "Tools/Color Catalog")]
    public sealed class ColorCatalog : ScriptableObject
    {
        [SerializeField] private List<ColorGroup> _groups = new();

        public IReadOnlyList<ColorGroup> Groups => _groups;

#if UNITY_EDITOR
        [Button("Refresh")]
        private void Refresh()
        {
            // FrameworkMenu.Codegen + "Colors" (ColorGenerator.Generate): the runtime assembly has no editor constants.
            EditorApplication.ExecuteMenuItem("Tools/Framework/Codegen/Colors");
        }
#endif
    }
}