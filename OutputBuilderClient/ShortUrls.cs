using System.Collections.Concurrent;

namespace OutputBuilderClient
{
    internal static class ShortUrls
    {
        private static readonly ConcurrentDictionary<string, string> ShorternedUrls =
            new ConcurrentDictionary<string, string>();

        public static int Count { get { return ShorternedUrls.Count; } }

        public static bool TryAdd(string longUrl, string shortUrl)
        {
            return ShorternedUrls.TryAdd(longUrl, shortUrl);
        }

        public static bool TryGetValue(string url, out string shortUrl)
        {
            return ShorternedUrls.TryGetValue(url, out shortUrl);
        }
    }
}