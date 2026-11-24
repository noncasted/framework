using System;
using Cysharp.Threading.Tasks;
using UnityEditor;

namespace Internal
{
    // The generated catalogues of the game outside Play mode (an editor tool, an EditMode test): bound to the assets of the
    // project as in Play mode, the main bundle by its name, until the lifetime ends. A group is then retained and read as in
    // a run. Play mode binds them with AddContent of the root scope.
    public static class EditorCatalogues
    {
        public static async UniTask Bind(IReadOnlyLifetime lifetime, string mainBundle)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
                throw new InvalidOperationException("[EditorCatalogues] Play mode binds the catalogues with AddContent");

            var loader = await ContentEntryPoint.RegisterMain(lifetime, new ContentOptions(mainBundle));
            await CatalogContent.Bind(lifetime, loader, loader.RootAssets<ContentRoot>());
        }
    }
}
