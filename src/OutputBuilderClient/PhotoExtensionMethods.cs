using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileNaming;
using ObjectModel;
using OutputBuilderClient.Interfaces;

namespace OutputBuilderClient
{
    internal static class PhotoExtensionMethods
    {
        public static void UpdateTargetWithSourceProperties(this Photo targetPhoto, Photo sourcePhoto)
        {
            targetPhoto.Version = sourcePhoto.Version;
            targetPhoto.UrlSafePath = sourcePhoto.UrlSafePath;
            targetPhoto.BasePath = sourcePhoto.BasePath;
            targetPhoto.PathHash = sourcePhoto.PathHash;
            targetPhoto.ImageExtension = sourcePhoto.ImageExtension;
            targetPhoto.Files = sourcePhoto.Files;
            targetPhoto.Metadata = sourcePhoto.Metadata;
            targetPhoto.ImageSizes = sourcePhoto.ImageSizes;
            targetPhoto.ShortUrl = sourcePhoto.ShortUrl;
        }

        public static Task UpdateFileHashesAsync(this Photo targetPhoto, Photo sourcePhoto, ISettings settings)
        {
            if (targetPhoto != null)
            {
                foreach (ComponentFile sourceFile in sourcePhoto.Files.Where(predicate: s => string.IsNullOrWhiteSpace(s.Hash)))
                {
                    ComponentFile targetFile = targetPhoto.Files.FirstOrDefault(predicate: s => s.Extension == sourceFile.Extension && !string.IsNullOrWhiteSpace(s.Hash));

                    if (targetFile != null)
                    {
                        sourceFile.Hash = targetFile.Hash;
                    }
                }
            }

            return Task.WhenAll(sourcePhoto.Files.Where(predicate: s => string.IsNullOrWhiteSpace(s.Hash))
                                           .Select(selector: x => sourcePhoto.SetFileHashAsync(x, settings)));
        }

        private static async Task SetFileHashAsync(this Photo sourcePhoto, ComponentFile file, ISettings settings)
        {
            string filename = Path.Combine(settings.RootFolder, sourcePhoto.BasePath + file.Extension);

            file.Hash = await Hasher.HashFileAsync(filename);
        }
    }
}