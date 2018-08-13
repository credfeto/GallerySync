using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileNaming;
using ObjectModel;

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

        public static Task UpdateFileHashes(this Photo targetPhoto, Photo sourcePhoto)
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
                                    .Select(sourcePhoto.SetFileHash));
        }

        private static async Task SetFileHash(this Photo sourcePhoto, ComponentFile file)
        {
            string filename = Path.Combine(Settings.Default.RootFolder, sourcePhoto.BasePath + file.Extension);

            file.Hash = await Hasher.HashFile(filename);
        }
    }
}