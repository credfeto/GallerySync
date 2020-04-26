using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImageLoader.Interfaces;
using Images;
using Microsoft.Extensions.Logging;
using ObjectModel;
using StorageHelpers;

namespace OutputBuilderClient.Services
{
    public sealed class GalleryBuilder : IGalleryBuilder
    {
        private static readonly SemaphoreSlim Sempahore = new SemaphoreSlim(initialCount: 1);
        private readonly IImageExtraction _imageExtraction;
        private readonly IImageLoader _imageLoader;
        private readonly ILogger<GalleryBuilder> _logging;
        private readonly IRebuildDetection _rebuildDetection;

        public GalleryBuilder(IImageLoader imageLoader, IImageExtraction imageExtraction, IRebuildDetection rebuildDetection, ILogger<GalleryBuilder> logging)
        {
            this._imageLoader = imageLoader;
            this._imageExtraction = imageExtraction;
            this._rebuildDetection = rebuildDetection;
            this._logging = logging;
        }

        public async Task ProcessGalleryAsync(ISettings imageSettings)
        {
            Task<Photo[]> sourceTask = PhotoMetadataRepository.LoadEmptyRepositoryAsync(Settings.RootFolder, this._logging);
            Task<Photo[]> targetTask = PhotoMetadataRepository.LoadRepositoryAsync(Settings.DatabaseOutputFolder, this._logging);

            Photo[][] results = await Task.WhenAll(sourceTask, targetTask);

            Photo[] source = results[0];
            Photo[] target = results[1];

            await this.ProcessAsync(source, target, imageSettings);
        }

        private async Task<HashSet<string>> ProcessAsync(Photo[] source, Photo[] target, ISettings imageSettings)
        {
            ConcurrentDictionary<string, bool> items = new ConcurrentDictionary<string, bool>();

            await Task.WhenAll(source.Select(selector: sourcePhoto => this.ProcessSinglePhotoAsync(target, sourcePhoto, items, imageSettings))
                                     .ToArray());

            return new HashSet<string>(items.Keys);
        }

        private async Task ProcessSinglePhotoAsync(Photo[] target, Photo sourcePhoto, ConcurrentDictionary<string, bool> items, ISettings imageSettings)
        {
            ForceGarbageCollection();

            try
            {
                Photo targetPhoto = target.FirstOrDefault(predicate: item => item.PathHash == sourcePhoto.PathHash);
                bool build = targetPhoto == null;
                bool rebuild = targetPhoto != null && await this._rebuildDetection.NeedsFullResizedImageRebuildAsync(sourcePhoto, targetPhoto, imageSettings, this._logging);
                bool rebuildMetadata = targetPhoto != null && this._rebuildDetection.MetadataVersionOutOfDate(targetPhoto, this._logging);

                string url = "https://www.markridgwell.co.uk/albums/" + sourcePhoto.UrlSafePath;
                string shortUrl;

                if (targetPhoto != null)
                {
                    shortUrl = targetPhoto.ShortUrl;

                    if (ShortUrls.ShouldGenerateShortUrl(sourcePhoto, shortUrl, url))
                    {
                        shortUrl = await this.TryGenerateShortUrlAsync(url);

                        if (!StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                        {
                            ShortUrls.LogShortUrl(url, shortUrl, imageSettings);

                            rebuild = true;
                            this._logging.LogInformation($" +++ Force rebuild: missing shortcut URL.  New short url: {shortUrl}");
                        }
                    }
                }
                else
                {
                    if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                    {
                        this._logging.LogInformation($"* Reusing existing short url: {shortUrl}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(shortUrl) && !StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                {
                    sourcePhoto.ShortUrl = shortUrl;
                }
                else
                {
                    shortUrl = Constants.DefaultShortUrl;
                }

                if (build || rebuild || rebuildMetadata)
                {
                    await this.ProcessOneFileAsync(sourcePhoto, targetPhoto, rebuild, rebuildMetadata, url, shortUrl, imageSettings);
                }
                else
                {
                    this._logging.LogInformation($"Unchanged: {targetPhoto.UrlSafePath}");
                }

                items.TryAdd(sourcePhoto.PathHash, value: true);
            }
            catch (AbortProcessingException exception)
            {
                BrokenImages.LogBrokenImage(sourcePhoto.UrlSafePath, exception);

                throw;
            }
            catch (StackOverflowException exception)
            {
                BrokenImages.LogBrokenImage(sourcePhoto.UrlSafePath, exception);

                throw;
            }
            catch (Exception exception)
            {
                BrokenImages.LogBrokenImage(sourcePhoto.UrlSafePath, exception);
            }
        }

        private static void ForceGarbageCollection()
        {
            GC.GetTotalMemory(forceFullCollection: true);
        }

        private async Task ProcessOneFileAsync(Photo sourcePhoto, Photo targetPhoto, bool rebuild, bool rebuildMetadata, string url, string shortUrl, ISettings imageSettings)
        {
            this._logging.LogInformation(rebuild ? $"Rebuild: {sourcePhoto.UrlSafePath}" : $"Build: {sourcePhoto.UrlSafePath}");

            await targetPhoto.UpdateFileHashesAsync(sourcePhoto);

            bool buildMetadata = targetPhoto == null || rebuild || rebuildMetadata || targetPhoto.Metadata == null;

            if (buildMetadata)
            {
                sourcePhoto.Metadata = MetadataExtraction.ExtractMetadata(sourcePhoto);
            }
            else
            {
                sourcePhoto.Metadata = targetPhoto.Metadata;
            }

            bool buildImages = targetPhoto == null || rebuild || !targetPhoto.ImageSizes.HasAny();

            List<string> filesCreated = new List<string>();

            if (buildImages)
            {
                this._logging.LogInformation(message: "Build images:");
                DateTime creationDate = MetadataHelpers.ExtractCreationDate(sourcePhoto.Metadata);

                try
                {
                    IReadOnlyList<ImageSize> sizes = await this._imageExtraction.BuildImagesAsync(sourcePhoto, filesCreated, creationDate, url, shortUrl, imageSettings);

                    sourcePhoto.ImageSizes = sizes.ToList();
                }
                catch (Exception exception)
                {
                    this._logging.LogInformation($" Failed to load image: {sourcePhoto.UrlSafePath}: {exception.Message}");

                    throw;
                }
            }
            else
            {
                this._logging.LogInformation(message: "Not building images");
                sourcePhoto.ImageSizes = targetPhoto.ImageSizes;
            }

            sourcePhoto.Version = Constants.CurrentMetadataVersion;

            if (targetPhoto != null)
            {
                targetPhoto.UpdateTargetWithSourceProperties(sourcePhoto);
                targetPhoto.Version = Constants.CurrentMetadataVersion;

                if (buildImages)
                {
                    //?
                }

                await PhotoMetadataRepository.StoreAsync(targetPhoto);
            }
            else
            {
                await PhotoMetadataRepository.StoreAsync(sourcePhoto);
            }
        }

        private async Task<string> TryGenerateShortUrlAsync(string url)
        {
            if (ShortUrls.TryGetValue(url, out string shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
            {
                return shortUrl;
            }

            await Sempahore.WaitAsync();

            if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
            {
                return shortUrl;
            }

            try
            {
                string filename = Settings.ShortNamesFile + ".tracking.json";

                List<ShortenerCount> tracking = new List<ShortenerCount>();

                if (File.Exists(filename))
                {
                    byte[] bytes = await FileHelpers.ReadAllBytesAsync(filename);

                    ShortenerCount[] items = JsonSerializer.Deserialize<ShortenerCount[]>(Encoding.UTF8.GetString(bytes));

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

                    await FileHelpers.WriteAllBytesAsync(filename, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tracking.ToArray())), commit: false);
                }
                else
                {
                    if (counter.Impressions < maxImpressionsPerMonth)
                    {
                        this._logging.LogInformation(message: "Bitly Impressions for {counter.Impressions}");
                        this._logging.LogInformation(message: "Bitly Impressions total {counter.TotalImpressionsEver}");
                        ++counter.Impressions;
                        ++counter.TotalImpressionsEver;

                        await FileHelpers.WriteAllBytesAsync(filename, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tracking.ToArray())), commit: false);
                    }
                }

                if (counter.Impressions < maxImpressionsPerMonth)
                {
                    Uri shortened = await BitlyUrlShortner.ShortenAsync(new Uri(url));

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
}