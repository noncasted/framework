namespace Internal
{
    // Own speed of one aseprite tag: its animation and every custom animation made from it play at this FPS
    // instead of the FPS of the file.
    public sealed class SpriteCatalogTagSpeed
    {
        public SpriteCatalogTagSpeed(string tag, float framesPerSecond)
        {
            Tag = tag;
            FramesPerSecond = framesPerSecond;
        }

        public string Tag { get; }
        public float FramesPerSecond { get; }
    }
}
