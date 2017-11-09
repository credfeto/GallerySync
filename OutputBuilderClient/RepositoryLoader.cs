using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    internal static class RepositoryLoader
    {
        public static async Task<Photo[]> LoadRepository(string baseFolder)
        {
            Console.WriteLine("Loading Repository from {0}...", baseFolder);
            var scores = new[]
            {
                ".info"
            };

            var sidecarFiles = new List<string>();

            var emitter = new PhotoInfoEmitter(baseFolder);

            if (Directory.Exists(baseFolder))
            {
                var filesFound = await DirectoryScanner.ScanFolder(baseFolder, emitter, scores.ToList(), sidecarFiles);

                Console.WriteLine("{0} : Files Found: {1}", baseFolder, filesFound);
            }

            return emitter.Photos;
        }

        public static async Task<Photo[]> LoadEmptyRepository(string baseFolder)
        {
            Console.WriteLine("Loading Repository from {0}...", baseFolder);

            var emitter = new RawFileInfoEmitter();

            var scores = new[]
            {
                ".xmp",
                ".jpg",
                ".cr2",
                ".mrw",
                ".rw2",
                ".tif",
                ".tiff",
                ".psd"
            };

            var sidecarFiles = new[]
            {
                ".xmp"
            };

            var filesFound =
                await DirectoryScanner.ScanFolder(baseFolder, emitter, scores.ToList(), sidecarFiles.ToList());

            Console.WriteLine("{0} : Files Found: {1}", baseFolder, filesFound);

            return emitter.Photos;
        }
    }
}