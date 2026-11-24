using Cysharp.Threading.Tasks;

namespace Global.Publisher
{
    // The saves of the player in the storage of a platform. Read once by Load, before any Get or Save: AddSaves awaits it
    // while the scope is built, so every service reads them loaded, from its constructor on.
    public interface ISaves
    {
        UniTask Load();
        T Get<T>() where T : class, new();
        UniTask Save<T>(T data);
    }
}
