namespace Internal
{
    // The bundle that holds an asset: as a root of it, or as a lazy target it reached first (where: the reference).
    public readonly struct ContentBundleClaim
    {
        public ContentBundleClaim(ContentBundle bundle, bool isRoot, string where)
        {
            Bundle = bundle;
            IsRoot = isRoot;
            Where = where;
        }

        public ContentBundle Bundle { get; }
        public bool IsRoot { get; }
        public string Where { get; }
    }
}
