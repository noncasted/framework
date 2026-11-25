namespace Internal
{
    // The phase after every OnUpdate of the same frame.
    public interface IPostUpdatable
    {
        void OnPostUpdate(float delta);
    }
}
