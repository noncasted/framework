using Cysharp.Threading.Tasks;
using Internal.EditorTests.Content;

namespace Internal.Tests
{
    // A preloaded group of AssetGroupTests, as the generated catalogues write one: loaded synchronously both on Retain and
    // on the first access, over the preloaded group asset of the ContentRoot of ContentBundleTestBuild.
    public sealed class TestPreloadedGroup : AssetGroup<ContentTestGroup>
    {
        private readonly string _guid;

        public TestPreloadedGroup(string guid)
        {
            _guid = guid;
        }

        public override bool IsPreloaded => true;

        public ContentTestGroup Loaded => Asset;

        protected override UniTask LoadGroup()
        {
            LoadImmediately();
            return UniTask.CompletedTask;
        }

        protected override void LoadImmediately()
        {
            LoadPreloadedAsset(_guid);
        }

        protected override void UnloadGroup()
        {
            UnloadAsset();
        }
    }
}
