using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using FileNaming;
using ObjectModel;
using Scanner;

namespace OutputBuilderClient
{
    public sealed class RawFileInfoEmitter : IFileEmitter
    {
        private readonly ConcurrentBag<Photo> _photos = new ConcurrentBag<Photo>();

        public Photo[] Photos
        {
            get { return _photos.OrderBy(x => x.UrlSafePath).ToArray(); }
        }

        public async Task FileFound(FileEntry entry)
        {
            var basePath = Path.Combine(entry.RelativeFolder, Path.GetFileNameWithoutExtension(entry.LocalFileName));

            var item = await CreatePhotoRecord(entry, basePath);

            Store(item);
        }

        private void Store(Photo photo)
        {
            _photos.Add(photo);
        }

        private static async Task<Photo> CreatePhotoRecord(FileEntry entry, string basePath)
        {
            var urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            var componentFiles = new List<ComponentFile>();

            var factory = Task<ComponentFile>.Factory;

            var tasks =
                entry.AlternateFileNames.Concat(new[] {entry.LocalFileName})
                    .Select(fileName => ReadComponentFile(factory, Path.Combine(entry.Folder, fileName)))
                    .ToArray();

            var item = new Photo
            {
                BasePath = basePath,
                UrlSafePath = urlSafePath,
                PathHash = Hasher.HashBytes(Encoding.UTF8.GetBytes(urlSafePath)),
                ImageExtension = Path.GetExtension(entry.LocalFileName),
                Files = componentFiles
            };

            await Task.WhenAll(tasks).ContinueWith(t => componentFiles.AddRange(t.Result));

            //Console.WriteLine("Found: {0}", basePath);

            return item;
        }

        private static Task<ComponentFile> ReadComponentFile(TaskFactory<ComponentFile> factory, string fileName)
        {
            return factory.StartNew(() => ReadComponentFileAsync(fileName));
        }

        private static ComponentFile ReadComponentFileAsync(string fileName)
        {
            var info = new FileInfo(fileName);
            var extension = info.Extension.ToLowerInvariant();

            return new ComponentFile
            {
                Extension = extension,
                Hash = string.Empty,
                LastModified = info.LastWriteTimeUtc,
                FileSize = info.Length
            };
        }
    }
}