namespace Internal
{
    // One frame of a clip: the index in the strip of the clip itself (File null) or in another strip of its folder.
    internal readonly struct SpriteClipFrame
    {
        public SpriteClipFrame(string file, int index)
        {
            File = file;
            Index = index;
        }

        public string File { get; }
        public int Index { get; }
    }
}
