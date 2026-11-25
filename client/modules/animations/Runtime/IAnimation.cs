using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IAnimation
    {
        void Play(IReadOnlyLifetime lifetime, float time = 0);
        UniTask PlayAwaited(IReadOnlyLifetime lifetime, float time = 0);
        void PlayLooped(IReadOnlyLifetime lifetime, float time = 0);
    }
}
