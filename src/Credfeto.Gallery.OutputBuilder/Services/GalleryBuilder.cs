using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Images;
using Microsoft.Extensions.Logging;
using ObjectModel;
using OutputBuilderClient.Interfaces;

namespace OutputBuilderClient.Services
{
    public sealed class GalleryBuilder : IGalleryBuilder
    {
        private readonly IBrokenImageTracker _brokenImageTracker;
        private readonly IImageExtraction _imageExtraction;
        private readonly ILimitedUrlShortener _limitedUrlShortener;
        private readonly ILogger<GalleryBuilder> _logging;
        private readonly IRebuildDetection _rebuildDetection;
        private readonly ISettings _settings;
        private readonly IShortUrls _shortUrls;

        public GalleryBuilder(IImageExtraction imageExtraction,
                              IRebuildDetection rebuildDetection,
                              IShortUrls shortUrls,
                              ILimitedUrlShortener limitedUrlShortener,
                              IBrokenImageTracker brokenImageTracker,
                              ISettings settings,
                              ILogger<GalleryBuilder> logging)
        {
            this._imageExtraction = imageExtraction;
            this._rebuildDetection = rebuildDetection;
            this._shortUrls = shortUrls;
            this._limitedUrlShortener = limitedUrlShortener;
            this._brokenImageTracker = brokenImageTracker;
            this._settings = settings;
            this._logging = logging;
        }

        public async Task ProcessGalleryAsync(IImageSettings imageImageSettings)
        {
            Task<Photo[]> sourceTask = PhotoMetadataRepository.LoadEmptyRepositoryAsync(this._settings.RootFolder, this._logging);
            Task<Photo[]> targetTask = PhotoMetadataRepository.LoadRepositoryAsync(this._settings.DatabaseOutputFolder, this._logging);

            Photo[][] results = await Task.WhenAll(sourceTask, targetTask);

            Photo[] source = results[0];
            Photo[] target = results[1];

            await this.ProcessAsync(source, target, imageImageSettings);
        }

        private async Task<HashSet<string>> ProcessAsync(Photo[] source, Photo[] target, IImageSettings imageImageSettings)
        {
            ConcurrentDictionary<string, bool> items = new ConcurrentDictionary<string, bool>();

            await Task.WhenAll(source.Select(selector: sourcePhoto => this.ProcessSinglePhotoAsync(target, sourcePhoto, items, imageImageSettings))
                                     .ToArray());

            return new HashSet<string>(items.Keys);
        }

        private async Task ProcessSinglePhotoAsync(Photo[] target, Photo sourcePhoto, ConcurrentDictionary<string, bool> items, IImageSettings imageImageSettings)
        {
            ForceGarbageCollection();

            try
            {
                Photo targetPhoto = target.FirstOrDefault(predicate: item => item.PathHash == sourcePhoto.PathHash);
                bool build = targetPhoto == null;
                bool rebuild = targetPhoto != null && await this._rebuildDetection.NeedsFullResizedImageRebuildAsync(sourcePhoto, targetPhoto, imageImageSettings, this._logging);
                bool rebuildMetadata = targetPhoto != null && this._rebuildDetection.MetadataVersionOutOfDate(targetPhoto, this._logging);

                string url = "https://www.markridgwell.co.uk/albums/" + sourcePhoto.UrlSafePath;
                string shortUrl;

                if (targetPhoto != null)
                {
                    shortUrl = targetPhoto.ShortUrl;

                    if (this._shortUrls.ShouldGenerateShortUrl(sourcePhoto, shortUrl, url))
                    {
                        shortUrl = await this._limitedUrlShortener.TryGenerateShortUrlAsync(url);

                        if (!StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                        {
                            await this._shortUrls.LogShortUrlAsync(url, shortUrl);

                            rebuild = true;
                            this._logging.LogInformation($" +++ Force rebuild: missing shortcut URL.  New short url: {shortUrl}");
                        }
                    }
                }
                else
                {
                    if (this._shortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
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
                    await this.ProcessOneFileAsync(sourcePhoto, targetPhoto, rebuild, rebuildMetadata, url, shortUrl, imageImageSettings);
                }
                else
                {
                    this._logging.LogInformation($"Unchanged: {targetPhoto.UrlSafePath}");
                }

                items.TryAdd(sourcePhoto.PathHash, value: true);
            }
            catch (AbortProcessingException exception)
            {
                this._brokenImageTracker.LogBrokenImage(sourcePhoto.UrlSafePath, exception);

                throw;
            }
            catch (StackOverflowException exception)
            {
                this._brokenImageTracker.LogBrokenImage(sourcePhoto.UrlSafePath, exception);

                throw;
            }
            catch (Exception exception)
            {
                this._brokenImageTracker.LogBrokenImage(sourcePhoto.UrlSafePath, exception);
            }
        }

        private static void ForceGarbageCollection()
        {
            GC.GetTotalMemory(forceFullCollection: true);
        }

        private async Task ProcessOneFileAsync(Photo sourcePhoto,
                                               Photo targetPhoto,
                                               bool rebuild,
                                               bool rebuildMetadata,
                                               string url,
                                               string shortUrl,
                                               IImageSettings imageImageSettings)
        {
            this._logging.LogInformation(rebuild ? $"Rebuild: {sourcePhoto.UrlSafePath}" : $"Build: {sourcePhoto.UrlSafePath}");

            await targetPhoto.UpdateFileHashesAsync(sourcePhoto, this._settings);

            bool buildMetadata = targetPhoto == null || rebuild || rebuildMetadata || targetPhoto.Metadata == null;

            if (buildMetadata)
            {
                sourcePhoto.Metadata = MetadataExtraction.ExtractMetadata(sourcePhoto, this._settings);
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
                    IReadOnlyList<ImageSize> sizes = await this._imageExtraction.BuildImagesAsync(sourcePhoto, filesCreated, creationDate, url, shortUrl, imageImageSettings);

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

                await PhotoMetadataRepository.StoreAsync(targetPhoto, this._settings);
            }
            else
            {
                await PhotoMetadataRepository.StoreAsync(sourcePhoto, this._settings);
            }
        }
    }
}