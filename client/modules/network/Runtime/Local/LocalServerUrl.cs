using System;

namespace Internal
{
    public static class LocalServerUrl
    {
        public const string Scheme = "offline";

        public static string Create(string name)
        {
            return $"{Scheme}://{name}";
        }

        public static bool IsLocal(string url)
        {
            return url.StartsWith(Scheme + "://", StringComparison.Ordinal) == true;
        }

        public static string GetName(string url)
        {
            return new Uri(url).Host;
        }
    }
}
