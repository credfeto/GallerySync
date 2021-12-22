using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Microsoft.Extensions.Logging;

namespace Credfeto.Gallery.OutputBuilder.Services;

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
        return this._shorternedUrls.TryAdd(key: longUrl, value: shortUrl);
    }

    public bool TryGetValue(string url, out string shortUrl)
    {
        return this._shorternedUrls.TryGetValue(key: url, value: out shortUrl);
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
            if (!line.StartsWith(value: @"http://", comparisonType: StringComparison.OrdinalIgnoreCase) && !line.StartsWith(value: @"https://", comparisonType: StringComparison.OrdinalIgnoreCase))
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
        if (sourcePhoto.UrlSafePath.StartsWith(value: "private/", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(shortUrl) || StringComparer.InvariantCultureIgnoreCase.Equals(x: shortUrl, y: url) ||
               StringComparer.InvariantCultureIgnoreCase.Equals(x: shortUrl, y: Constants.DefaultShortUrl);
    }

    public async Task LogShortUrlAsync(string url, string shortUrl)
    {
        if (!this.TryAdd(longUrl: url, shortUrl: shortUrl))
        {
            return;
        }

        string[] text = { string.Format(format: "{0}\t{1}", arg0: url, arg1: shortUrl) };

        await File.AppendAllLinesAsync(path: this._settings.ShortNamesFile, contents: text);
    }
}