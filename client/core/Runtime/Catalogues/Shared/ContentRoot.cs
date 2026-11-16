using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // The root asset of the catalogues, a root of the main content bundle (ContentOptions.Name), written by
    // ContentRootGenerator into the content folder of the game: the group assets of the catalogues and the scenes of the
    // scene folders, all lazy. It holds no direct reference: a registered bundle is loaded whole and held, so a root stays thin.
    // The generated catalogues find their entries by the GUID of the asset (CatalogContent).
    public sealed class ContentRoot : ScriptableObject
    {
        [SerializeField] private List<ContentReference<ScriptableObject>> _groups = new();
        [SerializeField] private List<ContentReference<ScriptableObject>> _preloadedGroups = new();
        [SerializeField] private List<ContentScene> _scenes = new();

        // Loaded on Retain.
        public IReadOnlyList<ContentReference<ScriptableObject>> Groups => _groups;

        // Fetched while AddContent binds the catalogues, loaded synchronously on the first access.
        public IReadOnlyList<ContentReference<ScriptableObject>> PreloadedGroups => _preloadedGroups;

        public IReadOnlyList<ContentScene> Scenes => _scenes;

        public ContentReference<ScriptableObject> Group(string guid)
        {
            foreach (var group in _groups)
            {
                if (group.Guid == guid)
                    return group;
            }

            foreach (var group in _preloadedGroups)
            {
                if (group.Guid == guid)
                    return group;
            }

            throw new KeyNotFoundException($"[ContentRoot] No group {guid} in {name}: regenerate the catalogues (Regenerate All)");
        }

        public ContentScene Scene(string guid)
        {
            foreach (var scene in _scenes)
            {
                if (scene.Guid == guid)
                    return scene;
            }

            throw new KeyNotFoundException($"[ContentRoot] No scene {guid} in {name}: regenerate the catalogues (Regenerate All)");
        }
    }
}
