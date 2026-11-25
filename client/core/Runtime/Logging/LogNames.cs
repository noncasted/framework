using System;
using System.Text;

namespace Internal
{
    // The names of the categories: the namespace and the type, nested types after a dot, generic arguments by their short
    // names ("Global.Network.Pool<Packet>").
    public static class LogNames
    {
        public static string Of(Type type)
        {
            var builder = new StringBuilder();

            if (string.IsNullOrEmpty(type.Namespace) == false)
                builder.Append(type.Namespace).Append('.');

            AppendDeclaring(builder, type);
            builder.Append(ShortOf(type));
            return builder.ToString();
        }

        public static string ShortOf(Type type)
        {
            if (type.IsGenericType == false)
                return type.Name;

            var builder = new StringBuilder();
            var name = type.Name;
            var tick = name.IndexOf('`');

            builder.Append(tick < 0 ? name : name.Substring(0, tick)).Append('<');

            var arguments = type.GetGenericArguments();

            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(ShortOf(arguments[i]));
            }

            return builder.Append('>').ToString();
        }

        private static void AppendDeclaring(StringBuilder builder, Type type)
        {
            if (type.DeclaringType == null || type.IsGenericParameter)
                return;

            AppendDeclaring(builder, type.DeclaringType);
            builder.Append(ShortOf(type.DeclaringType)).Append('.');
        }
    }
}
