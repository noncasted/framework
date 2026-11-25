namespace Internal
{
    public class UpdaterSample
    {
        public long Ticks { get; private set; }
        public int Calls { get; private set; }

        internal void Add(long ticks)
        {
            Ticks += ticks;
            Calls++;
        }

        internal void Reset()
        {
            Ticks = 0;
            Calls = 0;
        }
    }
}
