using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Gallery.Image;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Credfeto.Gallery.Repository;
using Microsoft.Extensions.Logging;

namespace Credfeto.Gallery.OutputBuilder.Services
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
            Task<Photo[]> sourceTask = PhotoMetadataRepository.LoadEmptyRepositoryAsync(baseFolder: this._settings.RootFolder, logging: this._logging);
            Task<Photo[]> targetTask = PhotoMetadataRepository.LoadRepositoryAsync(baseFolder: this._settings.DatabaseOutputFolder, logging: this._logging);

            Photo[][] results = await Task.WhenAll(sourceTask, targetTask);

            Photo[] source = results[0];
            Photo[] target = results[1];

            await this.ProcessAsync(source: source, target: target, imageImageSettings: imageImageSettings);
        }

        private async Task<HashSet<string>> ProcessAsync(Photo[] source, Photo[] target, IImageSettings imageImageSettings)
        {
            ConcurrentDictionary<string, bool> items = new();

            await Task.WhenAll(source.Select(selector: sourcePhoto => this.ProcessSinglePhotoAsync(target: target, sourcePhoto: sourcePhoto, items: items, imageImageSettings: imageImageSettings))
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
                bool rebuild = targetPhoto != null &&
                               await this._rebuildDetection.NeedsFullResizedImageRebuildAsync(sourcePhoto: sourcePhoto,
                                                                                              targetPhoto: targetPhoto,
                                                                                              imageImageSettings: imageImageSettings,
                                                                                              logging: this._logging);
                bool rebuildMetadata = targetPhoto != null && this._rebuildDetection.MetadataVersionOutOfDate(targetPhoto: targetPhoto, logging: this._logging);

                string url = "https://www.markridgwell.co.uk/albums/" + sourcePhoto.UrlSafePath;
                string shortUrl;

                if (targetPhoto != null)
                {
                    shortUrl = targetPhoto.ShortUrl;

                    if (this._shortUrls.ShouldGenerateShortUrl(sourcePhoto: sourcePhoto, shortUrl: shortUrl, url: url))
                    {
                        shortUrl = await this._limitedUrlShortener.TryGenerateShortUrlAsync(url);

                        if (!StringComparer.InvariantCultureIgnoreCase.Equals(x: shortUrl, y: url))
                        {
                            await this._shortUrls.LogShortUrlAsync(url: url, shortUrl: shortUrl);

                            rebuild = true;
                            this._logging.LogInformation($" +++ Force rebuild: missing shortcut URL.  New short url: {shortUrl}");
                        }
                    }
                }
                else
                {
                    if (this._shortUrls.TryGetValue(url: url, shortUrl: out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                    {
                        this._logging.LogInformation($"* Reusing existing short url: {shortUrl}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(shortUrl) && !StringComparer.InvariantCultureIgnoreCase.Equals(x: shortUrl, y: url))
                {
                    sourcePhoto.ShortUrl = shortUrl;
                }
                else
                {
                    shortUrl = Constants.DEFAULT_SHORT_URL;
                }

                if (build || rebuild || rebuildMetadata)
                {
                    await this.ProcessOneFileAsync(sourcePhoto: sourcePhoto,
                                                   targetPhoto: targetPhoto,
                                                   rebuild: rebuild,
                                                   rebuildMetadata: rebuildMetadata,
                                                   url: url,
                                                   shortUrl: shortUrl,
                                                   imageImageSettings: imageImageSettings);
                }
                else
                {
                    this._logging.LogInformation($"Unchanged: {targetPhoto.UrlSafePath}");
                }

                items.TryAdd(key: sourcePhoto.PathHash, value: true);
            }
            catch (AbortProcessingException exception)
            {
                this._brokenImageTracker.LogBrokenImage(path: sourcePhoto.UrlSafePath, exception: exception);

                throw;
            }
            catch (StackOverflowException exception)
            {
                this._brokenImageTracker.LogBrokenImage(path: sourcePhoto.UrlSafePath, exception: exception);

                throw;
            }
            catch (Exception exception)
            {
                this._brokenImageTracker.LogBrokenImage(path: sourcePhoto.UrlSafePath, exception: exception);
            }
        }

        private static void ForceGarbageCollection()
        {
            GC.GetTotalMemory(forceFullCollection: true);
        }

        private async Task ProcessOneFileAsync(Photo sourcePhoto, Photo targetPhoto, bool rebuild, bool rebuildMetadata, string url, string shortUrl, IImageSettings imageImageSettings)
        {
            this._logging.LogInformation(rebuild ? $"Rebuild: {sourcePhoto.UrlSafePath}" : $"Build: {sourcePhoto.UrlSafePath}");

            await targetPhoto.UpdateFileHashesAsync(sourcePhoto: sourcePhoto, settings: this._settings);

            bool buildMetadata = targetPhoto == null || rebuild || rebuildMetadata || targetPhoto.Metadata == null;

            if (buildMetadata)
            {
                sourcePhoto.Metadata = MetadataExtraction.ExtractMetadata(sourcePhoto: sourcePhoto, settings: this._settings);
            }
            else
            {
                sourcePhoto.Metadata = targetPhoto.Metadata;
            }

            bool buildImages = targetPhoto == null || rebuild || !targetPhoto.ImageSizes.HasAny();

            List<string> filesCreated = new();

            if (buildImages)
            {
                this._logging.LogInformation(message: "Build images:");
                DateTime creationDate = MetadataHelpers.ExtractCreationDate(sourcePhoto.Metadata);

                try
                {
                    IReadOnlyList<ImageSize> sizes = await this._imageExtraction.BuildImagesAsync(sourcePhoto: sourcePhoto,
                                                                                                  filesCreated: filesCreated,
                                                                                                  creationDate: creationDate,
                                                                                                  url: url,
                                                                                                  shortUrl: shortUrl,
                                                                                                  imageSettings: imageImageSettings);

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

            sourcePhoto.Version = Constants.CURRENT_METADATA_VERSION;

            if (targetPhoto != null)
            {
                targetPhoto.UpdateTargetWithSourceProperties(sourcePhoto);
                targetPhoto.Version = Constants.CURRENT_METADATA_VERSION;

                if (buildImages)
                {
                    //?
                }

                await PhotoMetadataRepository.StoreAsync(photo: targetPhoto, databaseOutputFolder: this._settings.DatabaseOutputFolder);
            }
            else
            {
                await PhotoMetadataRepository.StoreAsync(photo: sourcePhoto, databaseOutputFolder: this._settings.DatabaseOutputFolder);
            }
        }
    }
}