using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Microsoft.Extensions.Logging;

namespace Credfeto.Gallery.OutputBuilder.Services
{
    public sealed class ShortUrls : IShortUrls
    {
        private readonly ILogger<ShortUrls> _logging;
        private readonly ISettings _settings;

        private readonly ConcurrentDictionary<string, string> _shorternedUrls;

        public ShortUrls(ISettings settings, ILogger<ShortUrls> logging)
        {
            this._settings = settings;
            this._logging = logging;

            this._shorternedUrls = new ConcurrentDictionary<string, string>();
        }

        public int Count => this._shorternedUrls.Count;

        public bool TryAdd(string longUrl, string shortUrl)
        {
            return this._shorternedUrls.TryAdd(longUrl, shortUrl);
        }

        public bool TryGetValue(string url, out string shortUrl)
        {
            return this._shorternedUrls.TryGetValue(url, out shortUrl);
        }

        public async Task LoadAsync()
        {
            string logPath = this._settings.ShortNamesFile;

            if (!File.Exists(logPath))
            {
                return;
            }

            this._logging.LogInformation(message: "Loading Existing Short Urls:");
            string[] lines = await File.ReadAllLinesAsync(logPath);

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

                if (this.TryAdd(process[0], process[1]))
                {
                    this._logging.LogDebug(message: "Loaded Short Url {process[1]} for {process[0]}");
                }
            }

            this._logging.LogInformation($"Total Known Short Urls: {this.Count}");
        }

        public bool ShouldGenerateShortUrl(Photo sourcePhoto, string shortUrl, string url)
        {
            // ONly want to generate a short URL, IF the photo has already been uploaded AND is public
            if (sourcePhoto.UrlSafePath.StartsWith(value: "private/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(shortUrl) || StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url) ||
                   StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, Constants.DefaultShortUrl);
        }

        public async Task LogShortUrlAsync(string url, string shortUrl)
        {
            if (!this.TryAdd(url, shortUrl))
            {
                return;
            }

            string[] text = {string.Format(format: "{0}\t{1}", url, shortUrl)};

            await File.AppendAllLinesAsync(this._settings.ShortNamesFile, text);
        }
    }
}