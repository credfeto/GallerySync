using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Credfeto.Gallery.OutputBuilder.Services.Emitters;
using Credfeto.Gallery.Scanner;
using Credfeto.Gallery.Storage;
using Microsoft.Extensions.Logging;

namespace Credfeto.Gallery.OutputBuilder
{
    internal static class PhotoMetadataRepository
    {
        public static async Task<Photo[]> LoadRepositoryAsync(string baseFolder, ILogger logging)
        {
            logging.LogInformation($"Loading Repository from {baseFolder}...");
            string[] scores = {".info"};

            List<string> sidecarFiles = new List<string>();

            PhotoInfoEmitter emitter = new PhotoInfoEmitter(baseFolder);

            if (Directory.Exists(baseFolder))
            {
                long filesFound = await DirectoryScanner.ScanFolderAsync(baseFolder, emitter, scores.ToList(), sidecarFiles);

                logging.LogInformation($"{baseFolder} : Files Found: {filesFound}");
            }

            return emitter.Photos;
        }

        public static async Task<Photo[]> LoadEmptyRepositoryAsync(string baseFolder, ILogger logging)
        {
            logging.LogInformation($"Loading Repository from {baseFolder}...");

            RawFileInfoEmitter emitter = new RawFileInfoEmitter();

            string[] scores = {".xmp", ".jpg", ".cr2", ".mrw", ".rw2", ".tif", ".tiff", ".psd"};

            string[] sidecarFiles = {".xmp"};

            long filesFound = await DirectoryScanner.ScanFolderAsync(baseFolder, emitter, scores.ToList(), sidecarFiles.ToList());

            logging.LogInformation($"{baseFolder} : Files Found: {filesFound}");

            return emitter.Photos;
        }

        public static Task StoreAsync(Photo photo, ISettings settings)
        {
            string safeUrl = photo.UrlSafePath.Replace(oldChar: '/', Path.DirectorySeparatorChar);
            safeUrl = safeUrl.TrimEnd(Path.DirectorySeparatorChar);
            safeUrl += ".info";

            string outputPath = Path.Combine(settings.DatabaseOutputFolder, safeUrl);

            string txt = JsonSerializer.Serialize(photo);

            return FileHelpers.WriteAllBytesAsync(outputPath, Encoding.UTF8.GetBytes(txt), commit: true);
        }
    }
}