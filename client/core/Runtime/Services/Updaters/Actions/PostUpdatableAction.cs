using System;

namespace Internal
{
    // A callback on every PostUpdate of the updater (RunPostUpdate): the drawing of a state on the lifetime of its handle.
    public class PostUpdatableAction : IPostUpdatable
    {
        public PostUpdatableAction(Action<float> callback)
        {
            _callback = callback;
        }

        private readonly Action<float> _callback;

        public void OnPostUpdate(float delta)
        {
            _callback.Invoke(delta);
        }
    }
}
