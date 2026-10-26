namespace Internal
{
    public interface IFixedUpdatable
    {
        void OnFixedUpdate(float delta);
    }

    public interface IGizmosUpdatable
    {
        void OnGizmosUpdate();
    }

    public interface IPostFixedUpdatable
    {
        void OnPostFixedUpdate(float delta);
    }

    public interface IPreFixedUpdatable
    {
        void OnPreFixedUpdate(float delta);
    }

    public interface IPreUpdatable
    {
        void OnPreUpdate(float delta);
    }

    public interface IUpdatable
    {
        void OnUpdate(float delta);
    }

    // The phase after every OnUpdate of the same frame.
    public interface IPostUpdatable
    {
        void OnPostUpdate(float delta);
    }

    // The LateUpdate phase: after the animators and the ordinary Update of the scene.
    public interface ILateUpdatable
    {
        void OnLateUpdate(float delta);
    }
}