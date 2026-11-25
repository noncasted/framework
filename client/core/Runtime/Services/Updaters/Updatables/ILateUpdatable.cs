namespace Internal
{
    // The LateUpdate phase: after the animators and the ordinary Update of the scene.
    public interface ILateUpdatable
    {
        void OnLateUpdate(float delta);
    }
}
