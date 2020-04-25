using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileNaming;
using Images;
using Microsoft.Extensions.Logging;
using ObjectModel;

namespace OutputBuilderClient
{
    internal static class RebuildDetection
    {
        public static async Task<bool> NeedsFullResizedImageRebuildAsync(Photo sourcePhoto, Photo targetPhoto, ISettings imageSettings, ILogger logging)
        {
            return MetadataVersionRequiresRebuild(targetPhoto, logging) || await HaveFilesChangedAsync(sourcePhoto, targetPhoto, logging) ||
                   HasMissingResizes(targetPhoto, imageSettings, logging);
        }

        public static bool MetadataVersionOutOfDate(Photo targetPhoto, ILogger logging)
        {
            if (MetadataVersionHelpers.IsOutOfDate(targetPhoto.Version))
            {
                logging.LogInformation($" +++ Metadata update: Metadata version out of date. (Current: {targetPhoto.Version} Expected: {Constants.CurrentMetadataVersion})");

                return true;
            }

            return false;
        }

        public static async Task<bool> HaveFilesChangedAsync(Photo sourcePhoto, Photo targetPhoto, ILogger logging)
        {
            if (sourcePhoto.Files.Count != targetPhoto.Files.Count)
            {
                logging.LogInformation(message: " +++ Metadata update: File count changed");

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
                        logging.LogInformation($" +++ Metadata update: File size changed (File: {found.Extension})");

                        return true;
                    }

                    if (componentFile.LastModified == found.LastModified)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(found.Hash))
                    {
                        string filename = Path.Combine(Settings.RootFolder, sourcePhoto.BasePath + componentFile.Extension);

                        found.Hash = await Hasher.HashFileAsync(filename);
                    }

                    if (componentFile.Hash != found.Hash)
                    {
                        logging.LogInformation($" +++ Metadata update: File hash changed (File: {found.Extension})");

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

        private static bool HasMissingResizes(Photo photoToProcess, ISettings imageSettings, ILogger logging)
        {
            if (photoToProcess.ImageSizes == null)
            {
                Console.WriteLine(value: " +++ Force rebuild: No image sizes at all!");

                return true;
            }

            foreach (ImageSize resize in photoToProcess.ImageSizes)
            {
                string resizedFileName = Path.Combine(imageSettings.ImagesOutputPath,
                                                      HashNaming.PathifyHash(photoToProcess.PathHash),
                                                      ImageExtraction.IndividualResizeFileName(photoToProcess, resize));

                if (!File.Exists(resizedFileName))
                {
                    logging.LogInformation($" +++ Force rebuild: Missing image for size {resize.Width}x{resize.Height} (jpg)");

                    return true;
                }

                if (resize.Width == imageSettings.ThumbnailSize)
                {
                    resizedFileName = Path.Combine(imageSettings.ImagesOutputPath,
                                                   HashNaming.PathifyHash(photoToProcess.PathHash),
                                                   ImageExtraction.IndividualResizeFileName(photoToProcess, resize, extension: "png"));

                    if (!File.Exists(resizedFileName))
                    {
                        logging.LogInformation($" +++ Force rebuild: Missing image for size {resize.Width}x{resize.Height} (png)");

                        return true;
                    }
                }
            }

            return false;
        }

        public static bool MetadataVersionRequiresRebuild(Photo targetPhoto, ILogger logging)
        {
            if (MetadataVersionHelpers.RequiresRebuild(targetPhoto.Version))
            {
                logging.LogInformation($" +++ Metadata update: Metadata version Requires rebuild. (Current: {targetPhoto.Version} Expected: {Constants.CurrentMetadataVersion})");

                return true;
            }

            return false;
        }
    }
}