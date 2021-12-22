using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Credfeto.Gallery.Storage;
using Microsoft.Extensions.Logging;

namespace Credfeto.Gallery.OutputBuilder.Services;

public sealed class LimitedUrlShortenerer : ILimitedUrlShortener
{
    private static readonly SemaphoreSlim Sempahore = new(initialCount: 1);
    private readonly ILogger<LimitedUrlShortenerer> _logging;

    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ISettings _settings;
    private readonly IShortUrls _shortUrls;
    private readonly IUrlShortner _urlShortener;

    public LimitedUrlShortenerer(IUrlShortner urlShortener, IShortUrls shortUrls, ISettings settings, ILogger<LimitedUrlShortenerer> logging)
    {
        this._urlShortener = urlShortener;
        this._shortUrls = shortUrls;
        this._settings = settings;
        this._logging = logging;
        this._serializerOptions = new JsonSerializerOptions
                                  {
                                      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                      DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                                      IgnoreNullValues = true,
                                      WriteIndented = true,
                                      PropertyNameCaseInsensitive = true
                                  };
    }

    public async Task<string> TryGenerateShortUrlAsync(string url)
    {
        if (this._shortUrls.TryGetValue(url: url, out string shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
        {
            return shortUrl;
        }

        await Sempahore.WaitAsync();

        if (this._shortUrls.TryGetValue(url: url, shortUrl: out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
        {
            return shortUrl;
        }

        try
        {
            string filename = this._settings.ShortNamesFile + ".tracking.json";

            List<ShortenerCount> tracking = new();

            if (File.Exists(filename))
            {
                byte[] bytes = await FileHelpers.ReadAllBytesAsync(filename);

                ShortenerCount[] items = JsonSerializer.Deserialize<ShortenerCount[]>(Encoding.UTF8.GetString(bytes), options: this._serializerOptions);

                tracking.AddRange(items);
            }

            const int maxImpressionsPerMonth = 100;

            DateTime now = DateTime.UtcNow;

            ShortenerCount counter = tracking.FirstOrDefault(predicate: item => item.Year == now.Year && item.Month == now.Month);

            if (counter == null)
            {
                counter = new ShortenerCount();

                long totalImpressionsEver = 0L;

                foreach (ShortenerCount month in tracking)
                {
                    totalImpressionsEver += month.Impressions;
                }

                counter.Year = now.Year;
                counter.Month = now.Month;
                counter.Impressions = 1;
                counter.TotalImpressionsEver = totalImpressionsEver;

                tracking.Add(counter);

                await FileHelpers.WriteAllBytesAsync(fileName: filename, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tracking.ToArray(), options: this._serializerOptions)), commit: false);
            }
            else
            {
                if (counter.Impressions < maxImpressionsPerMonth)
                {
                    this._logging.LogInformation(message: "Bitly Impressions for {counter.Impressions}");
                    this._logging.LogInformation(message: "Bitly Impressions total {counter.TotalImpressionsEver}");
                    ++counter.Impressions;
                    ++counter.TotalImpressionsEver;

                    await FileHelpers.WriteAllBytesAsync(fileName: filename, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tracking.ToArray(), options: this._serializerOptions)), commit: false);
                }
            }

            if (counter.Impressions < maxImpressionsPerMonth)
            {
                Uri shortened = await this._urlShortener.ShortenAsync(new Uri(url));

                return shortened.ToString();
            }

            return url;
        }
        finally
        {
            Sempahore.Release();
        }
    }
}