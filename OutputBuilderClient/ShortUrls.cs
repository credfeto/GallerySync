using System;
using System.Collections.Concurrent;
using Alphaleonis.Win32.Filesystem;
using OutputBuilderClient.Properties;

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

        public static void LoadShortUrls()
        {
            var logPath = Settings.Default.ShortNamesFile;

            if (File.Exists(logPath))
            {
                Console.WriteLine("Loading Existing Short Urls:");
                var lines = File.ReadAllLines(logPath);

                foreach (var line in lines)
                {
                    if (!line.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase)
                        && !line.StartsWith(@"https://", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var process = line.Trim().Split('\t');
                    if (process.Length != 2)
                        continue;

                    if (ShortUrls.TryAdd(process[0], process[1]))
                    {
                        //Console.WriteLine("Loaded Short Url {0} for {1}", process[1], process[0]);
                    }
                }

                Console.WriteLine("Total Known Short Urls: {0}", ShortUrls.Count);
                Console.WriteLine();
            }
        }

        public static void LogShortUrl(string url, string shortUrl)
        {
            if (!ShortUrls.TryAdd(url, shortUrl)) return;

            var logPath = Path.Combine(Settings.Default.ImagesOutputPath, "ShortUrls.csv");

            var text = new[] {String.Format("{0}\t{1}", url, shortUrl)};

            File.AppendAllLines(logPath, text);
        }
    }
}