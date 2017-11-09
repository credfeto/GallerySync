using System.Collections.Concurrent;
using System.Linq;

namespace OutputBuilderClient
{
    internal static class BrokenImages
    {
        private static readonly ConcurrentDictionary<string, string> _brokenImages =
            new ConcurrentDictionary<string, string>();


        public static void LogBrokenImage(string path, string message)
        {
            _brokenImages.TryAdd(path, message);
        }

        public static string[] AllBrokenImages()
        {
            return _brokenImages.OrderBy(item => item.Key)
                .Select(item => string.Concat(item.Key, "\t", item.Value)).ToArray();
        }
    }
}