using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.Repository.Emitters;
using Credfeto.Gallery.Scanner;
using Credfeto.Gallery.Storage;
using Microsoft.Extensions.Logging;

namespace Credfeto.Gallery.Repository
{
    public static class PhotoMetadataRepository
    {
        private static readonly JsonSerializerOptions SerialiserOptions = new JsonSerializerOptions
                                                                          {
                                                                              PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                                                              DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                                                                              IgnoreNullValues = true,
                                                                              WriteIndented = true,
                                                                              PropertyNameCaseInsensitive = true
                                                                          };

        public static async Task<Photo[]> LoadRepositoryAsync(string baseFolder, ILogger logging)
        {
            logging.LogInformation($"Loading Repository from {baseFolder}...");
            string[] scores = {".info"};

            List<string> sidecarFiles = new List<string>();

            PhotoInfoEmitter emitter = new PhotoInfoEmitter(baseFolder);

            if (Directory.Exists(baseFolder))
            {
                long filesFound = await DirectoryScanner.ScanFolderAsync(baseFolder: baseFolder, fileEmitter: emitter, scores.ToList(), sidecarFiles: sidecarFiles);

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

            long filesFound = await DirectoryScanner.ScanFolderAsync(baseFolder: baseFolder, fileEmitter: emitter, extensionsToRetrieveInOrderOfPrecendence: scores, sidecarFiles: sidecarFiles);

            logging.LogInformation($"{baseFolder} : Files Found: {filesFound}");

            return emitter.Photos;
        }

        public static Task StoreAsync(Photo photo, string databaseOutputFolder)
        {
            string safeUrl = photo.UrlSafePath.Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar);
            safeUrl = safeUrl.TrimEnd(Path.DirectorySeparatorChar);
            safeUrl += ".info";

            string outputPath = Path.Combine(path1: databaseOutputFolder, path2: safeUrl);

            // TODO: use aysnc serialize
            string txt = JsonSerializer.Serialize(value: photo, options: SerialiserOptions);

            return FileHelpers.WriteAllBytesAsync(fileName: outputPath, Encoding.UTF8.GetBytes(txt), commit: true);
        }

        public static async Task<Photo> LoadAsync(string fileName)
        {
            byte[] bytes = await FileHelpers.ReadAllBytesAsync(fileName);

            // TODO: use aysnc deserialize
            return JsonSerializer.Deserialize<Photo>(Encoding.UTF8.GetString(bytes), options: SerialiserOptions);
        }
    }
}