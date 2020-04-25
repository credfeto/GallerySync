using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ObjectModel;
using Scanner;
using StorageHelpers;

namespace OutputBuilderClient
{
    internal static class PhotoMetadataRepository
    {
        public static async Task<Photo[]> LoadRepositoryAsync(string baseFolder)
        {
            await ConsoleOutput.LineAsync(formatString: "Loading Repository from {0}...", baseFolder);
            string[] scores = {".info"};

            List<string> sidecarFiles = new List<string>();

            PhotoInfoEmitter emitter = new PhotoInfoEmitter(baseFolder);

            if (Directory.Exists(baseFolder))
            {
                long filesFound = await DirectoryScanner.ScanFolderAsync(baseFolder, emitter, scores.ToList(), sidecarFiles);

                await ConsoleOutput.LineAsync(formatString: "{0} : Files Found: {1}", baseFolder, filesFound);
            }

            return emitter.Photos;
        }

        public static async Task<Photo[]> LoadEmptyRepositoryAsync(string baseFolder)
        {
            await ConsoleOutput.LineAsync(formatString: "Loading Repository from {0}...", baseFolder);

            RawFileInfoEmitter emitter = new RawFileInfoEmitter();

            string[] scores = {".xmp", ".jpg", ".cr2", ".mrw", ".rw2", ".tif", ".tiff", ".psd"};

            string[] sidecarFiles = {".xmp"};

            long filesFound = await DirectoryScanner.ScanFolderAsync(baseFolder, emitter, scores.ToList(), sidecarFiles.ToList());

            await ConsoleOutput.LineAsync(formatString: "{0} : Files Found: {1}", baseFolder, filesFound);

            return emitter.Photos;
        }

        public static Task StoreAsync(Photo photo)
        {
            string safeUrl = photo.UrlSafePath.Replace(oldChar: '/', Path.DirectorySeparatorChar);
            safeUrl = safeUrl.TrimEnd(Path.DirectorySeparatorChar);
            safeUrl += ".info";

            string outputPath = Path.Combine(Settings.DatabaseOutputFolder, safeUrl);

            string txt = JsonSerializer.Serialize(photo);

            return FileHelpers.WriteAllBytesAsync(outputPath, Encoding.UTF8.GetBytes(txt), true);
        }
    }
}