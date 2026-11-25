using System.Threading;

namespace Internal
{
    // The levels of one category, set again by every Logs.Configure; read from any thread.
    public class LogCategory
    {
        public LogCategory(string name, string shortName)
        {
            Name = name;
            ShortName = shortName;
        }

        private int _shown = (int)LogLevel.Info;
        private int _mark = (int)LogLevel.None;

        public string Name { get; }

        // The name of the type without its namespace: the prefix of a line in the console.
        public string ShortName { get; }

        public LogLevel Shown => (LogLevel)Volatile.Read(ref _shown);
        public LogLevel Mark => (LogLevel)Volatile.Read(ref _mark);

        public bool IsEnabled(LogLevel level)
        {
            if (level == LogLevel.None)
                return false;

            return (int)level >= Volatile.Read(ref _shown) || (int)level >= Volatile.Read(ref _mark);
        }

        public void SetLevels(LogLevel shown, LogLevel mark)
        {
            Volatile.Write(ref _shown, (int)shown);
            Volatile.Write(ref _mark, (int)mark);
        }
    }
}
