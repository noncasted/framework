using System;

namespace Internal
{
    internal static class GroupEntries
    {
        public static T Find<T>(T[] entries, string name, Func<T, string> getName) where T : class
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (string.Equals(getName(entries[i]), name, StringComparison.Ordinal))
                    return entries[i];
            }

            return null;
        }
    }
}
