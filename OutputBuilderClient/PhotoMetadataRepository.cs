using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using OutputBuilderClient.Properties;
using StorageHelpers;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    internal static class PhotoMetadataRepository
    {
        public static async Task<Photo[]> LoadRepository(string baseFolder)
        {
            await ConsoleOutput.Line("Loading Repository from {0}...", baseFolder);
            var scores = new[]
            {
                ".info"
            };

            var sidecarFiles = new List<string>();

            var emitter = new PhotoInfoEmitter(baseFolder);

            if (Directory.Exists(baseFolder))
            {
                var filesFound = await DirectoryScanner.ScanFolder(baseFolder, emitter, scores.ToList(), sidecarFiles);

                await ConsoleOutput.Line("{0} : Files Found: {1}", baseFolder, filesFound);
            }

            return emitter.Photos;
        }

        public static async Task<Photo[]> LoadEmptyRepository(string baseFolder)
        {
            await ConsoleOutput.Line("Loading Repository from {0}...", baseFolder);

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

            await ConsoleOutput.Line("{0} : Files Found: {1}", baseFolder, filesFound);

            return emitter.Photos;
        }

        public static Task Store(Photo photo)
        {
            var safeUrl = photo.UrlSafePath.Replace('/', Path.DirectorySeparatorChar);
            safeUrl = safeUrl.TrimEnd(Path.DirectorySeparatorChar);
            safeUrl += ".info";

            var outputPath = Path.Combine(
                Settings.Default.DatabaseOutputFolder,
                safeUrl
            );

            var txt = JsonConvert.SerializeObject(photo);
            return FileHelpers.WriteAllBytes(outputPath, Encoding.UTF8.GetBytes(txt));
        }
    }
}