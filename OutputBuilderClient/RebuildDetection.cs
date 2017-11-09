using System;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using FileNaming;
using OutputBuilderClient.Properties;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    internal static class RebuildDetection
    {
        public static async Task<bool> NeedsFullResizedImageRebuild(Photo sourcePhoto, Photo targetPhoto)
        {
            return await Program.MetadataVersionRequiresRebuild(targetPhoto) ||
                   await HaveFilesChanged(sourcePhoto, targetPhoto)
                   || HasMissingResizes(targetPhoto);
        }

        public static async Task<bool> MetadataVersionOutOfDate(Photo targetPhoto)
        {
            if (MetadataVersionHelpers.IsOutOfDate(targetPhoto.Version))
            {
                await ConsoleOutput.Line(
                    " +++ Metadata update: Metadata version out of date. (Current: " + targetPhoto.Version
                    + " Expected: " + Constants.CurrentMetadataVersion + ")");
                return true;
            }

            return false;
        }

        public static async Task<bool> HaveFilesChanged(Photo sourcePhoto, Photo targetPhoto)
        {
            if (sourcePhoto.Files.Count != targetPhoto.Files.Count)
            {
                await ConsoleOutput.Line(" +++ Metadata update: File count changed");
                return true;
            }

            foreach (var componentFile in targetPhoto.Files)
            {
                var found =
                    sourcePhoto.Files.FirstOrDefault(
                        candiate =>
                            StringComparer.InvariantCultureIgnoreCase.Equals((string) candiate.Extension,
                                componentFile.Extension));

                if (found != null)
                {
                    if (componentFile.FileSize != found.FileSize)
                    {
                        await ConsoleOutput.Line(" +++ Metadata update: File size changed (File: " + found.Extension +
                                                 ")");
                        return true;
                    }

                    if (componentFile.LastModified == found.LastModified)
                        continue;

                    if (string.IsNullOrWhiteSpace(found.Hash))
                    {
                        var filename = Path.Combine(
                            Settings.Default.RootFolder,
                            sourcePhoto.BasePath + componentFile.Extension);

                        found.Hash = await Hasher.HashFile(filename);
                    }

                    if (componentFile.Hash != found.Hash)
                    {
                        await ConsoleOutput.Line(" +++ Metadata update: File hash changed (File: " + found.Extension +
                                                 ")");
                        return true;
                    }
                }
                else
                {
                    await ConsoleOutput.Line(" +++ Metadata update: File missing (File: " + componentFile.Extension +
                                             ")");
                    return true;
                }
            }

            return false;
        }

        private static bool HasMissingResizes(Photo photoToProcess)
        {
            if (photoToProcess.ImageSizes == null)
            {
                Console.WriteLine(" +++ Force rebuild: No image sizes at all!");
                return true;
            }

            foreach (var resize in photoToProcess.ImageSizes)
            {
                var resizedFileName = Path.Combine(
                    Settings.Default.ImagesOutputPath,
                    HashNaming.PathifyHash(photoToProcess.PathHash),
                    ImageExtraction.IndividualResizeFileName(photoToProcess, resize));
                if (!File.Exists(resizedFileName))
                {
                    Console.WriteLine(
                        " +++ Force rebuild: Missing image for size {0}x{1} (jpg)",
                        resize.Width,
                        resize.Height);
                    return true;
                }

                // Moving this to a separate program
                //try
                //{
                //    byte[] bytes = Alphaleonis.Win32.Filesystem.File.ReadAllBytes(resizedFileName);

                //    if (!ImageHelpers.IsValidJpegImage(bytes, "Existing: " + resizedFileName))
                //    {
                //        Console.WriteLine(" +++ Force rebuild: image for size {0}x{1} is not a valid jpg", resize.Width,
                //                          resize.Height);
                //        return true;
                //    }
                //}
                //catch( Exception exception)
                //{
                //    Console.WriteLine(" +++ Force rebuild: image for size {0}x{1} is missing/corrupt - Exception: {2}", resize.Width,
                //                      resize.Height, exception.Message);
                //    return true;
                //}
                if (resize.Width == Settings.Default.ThumbnailSize)
                {
                    resizedFileName = Path.Combine(
                        Settings.Default.ImagesOutputPath,
                        HashNaming.PathifyHash(photoToProcess.PathHash),
                        ImageExtraction.IndividualResizeFileName(photoToProcess, resize, "png"));
                    if (!File.Exists(resizedFileName))
                    {
                        Console.WriteLine(
                            " +++ Force rebuild: Missing image for size {0}x{1} (png)",
                            resize.Width,
                            resize.Height);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}