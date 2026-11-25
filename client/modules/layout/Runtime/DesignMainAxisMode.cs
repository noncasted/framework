namespace Global.UI
{
    /// <summary>How the children are placed along the layout axis.</summary>
    public enum DesignMainAxisMode
    {
        /// <summary>Items keep their size, the gap between them is exactly <c>spacing</c>,
        /// the whole block is aligned by the main axis pivot.</summary>
        Group,

        /// <summary>Items keep their size, the free space is split evenly between them.</summary>
        Distribute,

        /// <summary>Items are resized so the content fills the container.</summary>
        Fill
    }
}
