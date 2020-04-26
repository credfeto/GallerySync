using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileNaming;
using Images;
using Microsoft.Extensions.Logging;
using ObjectModel;
using OutputBuilderClient.Interfaces;

namespace OutputBuilderClient.Services
{
    public sealed class RebuildDetection : IRebuildDetection
    {
        private readonly IImageFilenameGeneration _imageFilenameGeneration;
        private readonly ILogger<RebuildDetection> _logging;
        private readonly ISettings _settings;

        public RebuildDetection(IImageFilenameGeneration imageFilenameGeneration, ISettings settings, ILogger<RebuildDetection> logging)
        {
            this._imageFilenameGeneration = imageFilenameGeneration;
            this._settings = settings;
            this._logging = logging;
        }

        public async Task<bool> NeedsFullResizedImageRebuildAsync(Photo sourcePhoto, Photo targetPhoto, IImageSettings imageImageSettings, ILogger logging)
        {
            return this.MetadataVersionRequiresRebuild(targetPhoto, this._logging) || await this.HaveFilesChangedAsync(sourcePhoto, targetPhoto, this._logging) ||
                   this.HasMissingResizes(targetPhoto, imageImageSettings, this._logging);
        }

        public bool MetadataVersionOutOfDate(Photo targetPhoto, ILogger logging)
        {
            if (MetadataVersionHelpers.IsOutOfDate(targetPhoto.Version))
            {
                this._logging.LogInformation($" +++ Metadata update: Metadata version out of date. (Current: {targetPhoto.Version} Expected: {Constants.CurrentMetadataVersion})");

                return true;
            }

            return false;
        }

        public async Task<bool> HaveFilesChangedAsync(Photo sourcePhoto, Photo targetPhoto, ILogger logging)
        {
            if (sourcePhoto.Files.Count != targetPhoto.Files.Count)
            {
                this._logging.LogInformation(message: " +++ Metadata update: File count changed");

                return true;
            }

            foreach (ComponentFile componentFile in targetPhoto.Files)
            {
                ComponentFile found =
                    sourcePhoto.Files.FirstOrDefault(predicate: candiate => StringComparer.InvariantCultureIgnoreCase.Equals(candiate.Extension, componentFile.Extension));

                if (found != null)
                {
                    if (componentFile.FileSize != found.FileSize)
                    {
                        this._logging.LogInformation($" +++ Metadata update: File size changed (File: {found.Extension})");

                        return true;
                    }

                    if (componentFile.LastModified == found.LastModified)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(found.Hash))
                    {
                        string filename = Path.Combine(this._settings.RootFolder, sourcePhoto.BasePath + componentFile.Extension);

                        found.Hash = await Hasher.HashFileAsync(filename);
                    }

                    if (componentFile.Hash != found.Hash)
                    {
                        this._logging.LogInformation($" +++ Metadata update: File hash changed (File: {found.Extension})");

                        return true;
                    }
                }
                else
                {
                    logging.LogInformation($" +++ Metadata update: File missing (File: {componentFile.Extension})");

                    return true;
                }
            }

            return false;
        }

        public bool MetadataVersionRequiresRebuild(Photo targetPhoto, ILogger logging)
        {
            if (MetadataVersionHelpers.RequiresRebuild(targetPhoto.Version))
            {
                this._logging.LogInformation(
                    $" +++ Metadata update: Metadata version Requires rebuild. (Current: {targetPhoto.Version} Expected: {Constants.CurrentMetadataVersion})");

                return true;
            }

            return false;
        }

        private bool HasMissingResizes(Photo photoToProcess, IImageSettings imageImageSettings, ILogger logging)
        {
            if (photoToProcess.ImageSizes == null)
            {
                this._logging.LogInformation(message: " +++ Force rebuild: No image sizes at all!");

                return true;
            }

            foreach (ImageSize resize in photoToProcess.ImageSizes)
            {
                string resizedFileName = Path.Combine(imageImageSettings.ImagesOutputPath,
                                                      HashNaming.PathifyHash(photoToProcess.PathHash),
                                                      this._imageFilenameGeneration.IndividualResizeFileName(photoToProcess, resize));

                if (!File.Exists(resizedFileName))
                {
                    logging.LogInformation($" +++ Force rebuild: Missing image for size {resize.Width}x{resize.Height} (jpg)");

                    return true;
                }

                if (resize.Width == imageImageSettings.ThumbnailSize)
                {
                    resizedFileName = Path.Combine(imageImageSettings.ImagesOutputPath,
                                                   HashNaming.PathifyHash(photoToProcess.PathHash),
                                                   this._imageFilenameGeneration.IndividualResizeFileName(photoToProcess, resize, extension: "png"));

                    if (!File.Exists(resizedFileName))
                    {
                        this._logging.LogInformation($" +++ Force rebuild: Missing image for size {resize.Width}x{resize.Height} (png)");

                        return true;
                    }
                }
            }

            return false;
        }
    }
}