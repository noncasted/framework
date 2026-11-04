namespace Global.FrameRate
{
    // Outside a WebGL player nothing asks for a limit.
    public class NoFrameRateRequest : IFrameRateRequest
    {
        public bool TryGet(out int limit)
        {
            limit = 0;
            return false;
        }
    }
}
