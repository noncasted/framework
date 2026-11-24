namespace Internal
{
    // A clip of a PNG strip marked as Multiple Animations: its name in the group (<file><clip>), the frames of the strip
    // in the order they play and the seconds of one pass. Frames: empty - every frame in order; "2-4", "8-0" (a run down),
    // "1,0,1,2,3", a mix "0-3,5", frames of another strip of the folder "Close:3-0, 0-15, Close:0-3" (SpriteClipFrames).
    public sealed class SpriteCatalogClip
    {
        public SpriteCatalogClip(string name, string frames, float time)
        {
            Name = name;
            Frames = frames;
            Time = time;
        }

        public string Name { get; }
        public string Frames { get; }
        public float Time { get; }
    }
}
