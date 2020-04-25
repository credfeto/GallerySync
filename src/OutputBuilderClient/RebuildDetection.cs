﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileNaming;
using Images;
using ObjectModel;

namespace OutputBuilderClient
{
    internal static class RebuildDetection
    {
        public static async Task<bool> NeedsFullResizedImageRebuildAsync(Photo sourcePhoto, Photo targetPhoto, ISettings imageSettings)
        {
            return await MetadataVersionRequiresRebuildAsync(targetPhoto) || await HaveFilesChangedAsync(sourcePhoto, targetPhoto) || HasMissingResizes(targetPhoto, imageSettings);
        }

        public static async Task<bool> MetadataVersionOutOfDateAsync(Photo targetPhoto)
        {
            if (MetadataVersionHelpers.IsOutOfDate(targetPhoto.Version))
            {
                await ConsoleOutput.LineAsync(" +++ Metadata update: Metadata version out of date. (Current: " + targetPhoto.Version + " Expected: " +
                                              Constants.CurrentMetadataVersion + ")");

                return true;
            }

            return false;
        }

        public static async Task<bool> HaveFilesChangedAsync(Photo sourcePhoto, Photo targetPhoto)
        {
            if (sourcePhoto.Files.Count != targetPhoto.Files.Count)
            {
                await ConsoleOutput.LineAsync(formatString: " +++ Metadata update: File count changed");

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
                        await ConsoleOutput.LineAsync(" +++ Metadata update: File size changed (File: " + found.Extension + ")");

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
                        await ConsoleOutput.LineAsync(" +++ Metadata update: File hash changed (File: " + found.Extension + ")");

                        return true;
                    }
                }
                else
                {
                    await ConsoleOutput.LineAsync(" +++ Metadata update: File missing (File: " + componentFile.Extension + ")");

                    return true;
                }
            }

            return false;
        }

        private static bool HasMissingResizes(Photo photoToProcess, ISettings imageSettings)
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
                    Console.WriteLine(format: " +++ Force rebuild: Missing image for size {0}x{1} (jpg)", resize.Width, resize.Height);

                    return true;
                }

                if (resize.Width == imageSettings.ThumbnailSize)
                {
                    resizedFileName = Path.Combine(imageSettings.ImagesOutputPath,
                                                   HashNaming.PathifyHash(photoToProcess.PathHash),
                                                   ImageExtraction.IndividualResizeFileName(photoToProcess, resize, extension: "png"));

                    if (!File.Exists(resizedFileName))
                    {
                        Console.WriteLine(format: " +++ Force rebuild: Missing image for size {0}x{1} (png)", resize.Width, resize.Height);

                        return true;
                    }
                }
            }

            return false;
        }

        public static async Task<bool> MetadataVersionRequiresRebuildAsync(Photo targetPhoto)
        {
            if (MetadataVersionHelpers.RequiresRebuild(targetPhoto.Version))
            {
                await ConsoleOutput.LineAsync(" +++ Metadata update: Metadata version Requires rebuild. (Current: " + targetPhoto.Version + " Expected: " +
                                              Constants.CurrentMetadataVersion + ")");

                return true;
            }

            return false;
        }
    }
}