using System;
using System.Globalization;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;

namespace Internal
{
    // A template with its arguments: "{Name}" placeholders take the arguments by their order, "{{" and "}}" are the braces
    // themselves. The number of the placeholders is the number of the arguments, anything else throws. Runs only when the
    // level is on: the extensions of ILog check it first.
    public static class LogTemplate
    {
        private const int _maxKeptCapacity = 4096;

        [ThreadStatic]
        private static StringBuilder _builder;

        public static string Format(string template)
        {
            if (template.IndexOf('{') < 0 && template.IndexOf('}') < 0)
                return template;

            return Format<None, None, None, None>(template, 0, default, default, default, default);
        }

        public static string Format<T1>(string template, T1 a)
        {
            return Format<T1, None, None, None>(template, 1, a, default, default, default);
        }

        public static string Format<T1, T2>(string template, T1 a, T2 b)
        {
            return Format<T1, T2, None, None>(template, 2, a, b, default, default);
        }

        public static string Format<T1, T2, T3>(string template, T1 a, T2 b, T3 c)
        {
            return Format<T1, T2, T3, None>(template, 3, a, b, c, default);
        }

        public static string Format<T1, T2, T3, T4>(string template, T1 a, T2 b, T3 c, T4 d)
        {
            return Format(template, 4, a, b, c, d);
        }

        private static string Format<T1, T2, T3, T4>(string template, int count, T1 a, T2 b, T3 c, T4 d)
        {
            var builder = _builder ??= new StringBuilder(256);
            var index = 0;

            builder.Clear();

            for (var i = 0; i < template.Length; i++)
            {
                var symbol = template[i];

                if (symbol == '{' && At(template, i + 1) == '{' || symbol == '}' && At(template, i + 1) == '}')
                {
                    builder.Append(symbol);
                    i++;
                    continue;
                }

                if (symbol == '}')
                    throw new FormatException($"[Log] A lone '}}' at {i} of \"{template}\"");

                if (symbol != '{')
                {
                    builder.Append(symbol);
                    continue;
                }

                var end = template.IndexOf('}', i + 1);

                if (end < 0)
                    throw new FormatException($"[Log] An unclosed placeholder at {i} of \"{template}\"");

                if (index == count)
                    throw new FormatException($"[Log] More placeholders than the {count} arguments in \"{template}\"");

                switch (index)
                {
                    case 0: Append(builder, a); break;
                    case 1: Append(builder, b); break;
                    case 2: Append(builder, c); break;
                    default: Append(builder, d); break;
                }

                index++;
                i = end;
            }

            if (index != count)
                throw new FormatException($"[Log] {index} placeholders for the {count} arguments in \"{template}\"");

            var message = builder.ToString();

            if (builder.Capacity > _maxKeptCapacity)
                _builder = null;

            return message;
        }

        private static char At(string template, int index)
        {
            return index < template.Length ? template[index] : '\0';
        }

        // The common types without a boxing and in the invariant culture, the rest by their ToString.
        private static void Append<T>(StringBuilder builder, T value)
        {
            if (typeof(T) == typeof(string))
                builder.Append(UnsafeUtility.As<T, string>(ref value) ?? "null");
            else if (typeof(T) == typeof(int))
                builder.Append(UnsafeUtility.As<T, int>(ref value));
            else if (typeof(T) == typeof(long))
                builder.Append(UnsafeUtility.As<T, long>(ref value));
            else if (typeof(T) == typeof(bool))
                builder.Append(UnsafeUtility.As<T, bool>(ref value) ? "true" : "false");
            else if (typeof(T) == typeof(float))
                builder.Append(UnsafeUtility.As<T, float>(ref value).ToString(CultureInfo.InvariantCulture));
            else if (typeof(T) == typeof(double))
                builder.Append(UnsafeUtility.As<T, double>(ref value).ToString(CultureInfo.InvariantCulture));
            else if (value == null)
                builder.Append("null");
            else
                builder.Append(value.ToString());
        }

        // The type of an argument that is not there.
        private readonly struct None
        {
        }
    }
}
