using System;
using System.Collections.Concurrent;
using System.IO;
using Images;
using ObjectModel;

namespace OutputBuilderClient
{
    internal static class ShortUrls
    {
        private static readonly ConcurrentDictionary<string, string> ShorternedUrls = new ConcurrentDictionary<string, string>();

        public static int Count => ShorternedUrls.Count;

        public static bool TryAdd(string longUrl, string shortUrl)
        {
            return ShorternedUrls.TryAdd(longUrl, shortUrl);
        }

        public static bool TryGetValue(string url, out string shortUrl)
        {
            return ShorternedUrls.TryGetValue(url, out shortUrl);
        }

        public static void Load()
        {
            string logPath = Settings.ShortNamesFile;

            if (File.Exists(logPath))
            {
                Console.WriteLine(value: "Loading Existing Short Urls:");
                string[] lines = File.ReadAllLines(logPath);

                foreach (string line in lines)
                {
                    if (!line.StartsWith(value: @"http://", StringComparison.OrdinalIgnoreCase) && !line.StartsWith(value: @"https://", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] process = line.Trim()
                        .Split(separator: '\t');

                    if (process.Length != 2)
                    {
                        continue;
                    }

                    if (TryAdd(process[0], process[1]))
                    {
                        //Console.WriteLine("Loaded Short Url {0} for {1}", process[1], process[0]);
                    }
                }

                Console.WriteLine(format: "Total Known Short Urls: {0}", Count);
                Console.WriteLine();
            }
        }

        public static void LogShortUrl(string url, string shortUrl, ISettings imageSettings)
        {
            if (!TryAdd(url, shortUrl))
            {
                return;
            }

            string logPath = Path.Combine(imageSettings.ImagesOutputPath, path2: "ShortUrls.csv");

            string[] text = {string.Format(format: "{0}\t{1}", url, shortUrl)};

            File.AppendAllLines(logPath, text);
        }

        public static bool ShouldGenerateShortUrl(Photo sourcePhoto, string shortUrl, string url)
        {
            // ONly want to generate a short URL, IF the photo has already been uploaded AND is public
            if (sourcePhoto.UrlSafePath.StartsWith(value: "private/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(shortUrl) || StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url) ||
                   StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, Constants.DefaultShortUrl);
        }
    }
}