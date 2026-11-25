using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Internal
{
    public interface ILoadedScene
    {
        Scene Scene { get; }

        UniTask Unload();
    }
}