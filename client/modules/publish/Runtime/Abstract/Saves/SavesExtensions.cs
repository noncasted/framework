using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher
{
    public static class SavesExtensions
    {
        // The saves of a platform: read from its storage here, so the load is a step of the build of the scope (and of
        // its startup trace), before the services that read them exist.
        public static async UniTask AddSaves(this IScopeBuilder builder, ISaves saves)
        {
            await saves.Load();
            builder.RegisterInstance(saves);
        }
    }
}
