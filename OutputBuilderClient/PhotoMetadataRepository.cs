using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ObjectModel;
using OutputBuilderClient.Properties;
using Scanner;
using StorageHelpers;

namespace OutputBuilderClient
{
    internal static class PhotoMetadataRepository
    {
        public static async Task<Photo[]> LoadRepository(string baseFolder)
        {
            await ConsoleOutput.Line(formatString: "Loading Repository from {0}...", baseFolder);
            string[] scores = {".info"};

            List<string> sidecarFiles = new List<string>();

            PhotoInfoEmitter emitter = new PhotoInfoEmitter(baseFolder);

            if (Directory.Exists(baseFolder))
            {
                long filesFound = await DirectoryScanner.ScanFolder(baseFolder, emitter, scores.ToList(), sidecarFiles);

                await ConsoleOutput.Line(formatString: "{0} : Files Found: {1}", baseFolder, filesFound);
            }

            return emitter.Photos;
        }

        public static async Task<Photo[]> LoadEmptyRepository(string baseFolder)
        {
            await ConsoleOutput.Line(formatString: "Loading Repository from {0}...", baseFolder);

            RawFileInfoEmitter emitter = new RawFileInfoEmitter();

            string[] scores = {".xmp", ".jpg", ".cr2", ".mrw", ".rw2", ".tif", ".tiff", ".psd"};

            string[] sidecarFiles = {".xmp"};

            long filesFound = await DirectoryScanner.ScanFolder(baseFolder, emitter, scores.ToList(), sidecarFiles.ToList());

            await ConsoleOutput.Line(formatString: "{0} : Files Found: {1}", baseFolder, filesFound);

            return emitter.Photos;
        }

        public static Task Store(Photo photo)
        {
            string safeUrl = photo.UrlSafePath.Replace(oldChar: '/', Path.DirectorySeparatorChar);
            safeUrl = safeUrl.TrimEnd(Path.DirectorySeparatorChar);
            safeUrl += ".info";

            string outputPath = Path.Combine(Settings.Default.DatabaseOutputFolder, safeUrl);

            string txt = JsonConvert.SerializeObject(photo);

            return FileHelpers.WriteAllBytes(outputPath, Encoding.UTF8.GetBytes(txt));
        }
    }
}