namespace Global.Resolution
{
    // The platform side of the resolution setting: the page (render-resolution.js) turns the mode into the render pixel ratio
    // of the canvas.
    public interface IRenderPixelRatio
    {
        void Apply(ResolutionMode mode);
    }
}
