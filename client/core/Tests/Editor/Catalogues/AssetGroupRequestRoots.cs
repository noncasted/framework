using System;
using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    public static class AssetGroupRequestRoots
    {
        public static AssetGroup First { get; private set; }
        public static AssetGroup Second { get; private set; }

        public static void SetFirst(AssetGroup group)
        {
            First = group;
        }

        public static void SetSecond(AssetGroup group)
        {
            Second = group;
        }

        [ContainerGraphRoot]
        public static UniTask RequestThenFail(IScopeBuilder builder)
        {
            builder.RequestAssetGroup(First, "test");
            throw new InvalidOperationException("construct failed");
        }

        [ContainerGraphRoot]
        public static async UniTask LoadTwo(IScopeBuilder builder)
        {
            await builder.LoadAssetGroup(First, "test");
            await builder.LoadAssetGroup(Second, "test");
        }
    }
}
