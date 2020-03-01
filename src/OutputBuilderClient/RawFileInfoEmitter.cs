using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            get
            {
                return this._photos.OrderBy(keySelector: x => x.UrlSafePath)
                    .ToArray();
            }
        }

        public async Task FileFound(FileEntry entry)
        {
            string basePath = Path.Combine(entry.RelativeFolder, Path.GetFileNameWithoutExtension(entry.LocalFileName));

            Photo item = await CreatePhotoRecord(entry, basePath);

            this.Store(item);
        }

        private void Store(Photo photo)
        {
            this._photos.Add(photo);
        }

        private static async Task<Photo> CreatePhotoRecord(FileEntry entry, string basePath)
        {
            string urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            List<ComponentFile> componentFiles = new List<ComponentFile>();

            TaskFactory<ComponentFile> factory = Task<ComponentFile>.Factory;

            Task<ComponentFile>[] tasks = entry.AlternateFileNames.Concat(new[] {entry.LocalFileName})
                .Select(selector: fileName => ReadComponentFile(factory, Path.Combine(entry.Folder, fileName)))
                .ToArray();

            Photo item = new Photo
                         {
                             BasePath = basePath,
                             UrlSafePath = urlSafePath,
                             PathHash = Hasher.HashBytes(Encoding.UTF8.GetBytes(urlSafePath)),
                             ImageExtension = Path.GetExtension(entry.LocalFileName),
                             Files = componentFiles
                         };

            await Task.WhenAll(tasks)
                .ContinueWith(continuationAction: t => componentFiles.AddRange(t.Result));

            //Console.WriteLine("Found: {0}", basePath);

            return item;
        }

        private static Task<ComponentFile> ReadComponentFile(TaskFactory<ComponentFile> factory, string fileName)
        {
            return factory.StartNew(function: () => ReadComponentFileAsync(fileName));
        }

        private static ComponentFile ReadComponentFileAsync(string fileName)
        {
            FileInfo info = new FileInfo(fileName);
            string extension = info.Extension.ToLowerInvariant();

            return new ComponentFile {Extension = extension, Hash = string.Empty, LastModified = info.LastWriteTimeUtc, FileSize = info.Length};
        }
    }
}