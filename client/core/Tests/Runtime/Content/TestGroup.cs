using Cysharp.Threading.Tasks;
using Internal.EditorTests.Content;

namespace Internal.Tests
{
    // A group of AssetGroupTests loaded on Retain, as the generated catalogues write one, over the group asset of the
    // ContentRoot of ContentDirectoryTestBuild.
    public sealed class TestGroup : AssetGroup<ContentTestGroup>
    {
        private readonly string _guid;

        public TestGroup(string guid)
        {
            _guid = guid;
        }

        public ContentTestGroup Loaded => Asset;

        protected override async UniTask LoadGroup()
        {
            await LoadAsset(_guid);
        }

        protected override void UnloadGroup()
        {
            UnloadAsset();
        }
    }
}
