namespace Global.FrameRate
{
    // A limit asked from outside the game, stronger than the settings: ?fps= of the page (an A/B on production).
    public interface IFrameRateRequest
    {
        // False when nothing is asked; the limit is frames per second, or -1 for the rate of the screen (?fps=native).
        bool TryGet(out int limit);
    }
}
