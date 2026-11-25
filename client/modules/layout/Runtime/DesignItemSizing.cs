namespace Global.UI
{
    /// <summary>Per child override of the size it takes along the layout axis.</summary>
    public enum DesignItemSizing
    {
        /// <summary>Keep the size the child already has.</summary>
        Preferred,

        /// <summary>Take a fraction (0..1) of the container size.</summary>
        Percent,

        /// <summary>Share the free space with other weighted items. Used by <see cref="DesignMainAxisMode.Fill"/>.</summary>
        Weight,

        /// <summary>The child is not laid out at all.</summary>
        Ignore
    }
}
