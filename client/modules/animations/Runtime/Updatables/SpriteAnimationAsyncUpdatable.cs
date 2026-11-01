using Cysharp.Threading.Tasks;

namespace Internal
{
    public class SpriteAnimationAsyncUpdatable : IUpdatableSpriteAnimation
    {
        public SpriteAnimationAsyncUpdatable(SpriteAnimationVoidUpdatable voidUpdatable)
        {
            _voidUpdatable = voidUpdatable;
        }

        private readonly SpriteAnimationVoidUpdatable _voidUpdatable;

        private float _time;
        private UniTaskCompletionSource _completion;

        public UniTask Process(float time)
        {
            _time = time;
            _voidUpdatable.Start(_time);

            _completion?.TrySetCanceled();
            _completion = new UniTaskCompletionSource();

            return _completion.Task;
        }

        public bool Update(float deltaTime)
        {
            if (_voidUpdatable.Update(deltaTime) == false)
                return false;

            // Taken before it completes: TrySetResult runs the awaiter synchronously, and an awaiter that plays the
            // next PlayAwaited sets a new completion here - cleared after, that one was lost.
            var completion = _completion;
            _completion = null;
            completion.TrySetResult();

            return false;
        }

        public void Dispose()
        {
            _completion?.TrySetCanceled();
            _completion = null;
        }
    }
}