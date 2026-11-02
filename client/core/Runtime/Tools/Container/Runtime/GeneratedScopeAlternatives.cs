using System;

namespace Internal
{
    // The registrations under if / switch in a generated class.
    internal static class GeneratedScopeAlternatives
    {
        // An alternative whose branch did not run leaves its field null.
        public static T Require<T>(T value, string type) where T : class
        {
            if (value == null)
                throw new InvalidOperationException("No alternative of " + type + " was registered.");

            return value;
        }

        // A list without the alternatives whose branch did not run.
        public static T[] WithoutAbsent<T>(T[] values) where T : class
        {
            var count = 0;

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                    count++;
            }

            if (count == values.Length)
                return values;

            var result = new T[count];
            var next = 0;

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                    result[next++] = values[i];
            }

            return result;
        }
    }
}
