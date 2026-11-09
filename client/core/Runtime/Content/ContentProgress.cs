namespace Internal
{
    // The fetch of the files of one load, in bytes: what a loading screen shows. Total is 0 for content on the disk of
    // the player and in Play Mode of the editor, where nothing is fetched.
    public readonly struct ContentProgress
    {
        public ContentProgress(long loaded, long total)
        {
            Loaded = loaded;
            Total = total;
        }

        public long Loaded { get; }
        public long Total { get; }

        public bool IsDone => Loaded >= Total;
    }
}
