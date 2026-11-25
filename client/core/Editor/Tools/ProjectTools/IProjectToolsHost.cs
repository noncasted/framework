namespace Internal
{
    public interface IProjectToolsHost
    {
        OptionsContainer Options { get; }

        void SetStatus(string message, bool isError);

        // Redraws the window: IMGUI parts of a tab, progress of a long run.
        void Repaint();
    }
}
