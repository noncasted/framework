namespace Global.UI
{
    public enum DesignRefreshMode
    {
        /// <summary>Rebuild when the container, its children or their sizes change.</summary>
        OnChange,

        EveryFrame,

        /// <summary>Rebuild only on an explicit <c>Rebuild()</c> call (still previews in the editor).</summary>
        Manual
    }
}
