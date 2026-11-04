namespace Global.Resolution
{
    // Outside a WebGL player the screen keeps its resolution: the setting changes nothing there.
    public class NoRenderPixelRatio : IRenderPixelRatio
    {
        public void Apply(ResolutionMode mode)
        {
        }
    }
}
