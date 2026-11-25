namespace Global.UI
{
    /// <summary>How the children are sized across the layout axis.</summary>
    public enum DesignCrossAxisMode
    {
        /// <summary>The child keeps its own size and is aligned by the cross axis pivot.</summary>
        Keep,

        /// <summary>The child is stretched to the container size minus padding.</summary>
        Stretch
    }
}
