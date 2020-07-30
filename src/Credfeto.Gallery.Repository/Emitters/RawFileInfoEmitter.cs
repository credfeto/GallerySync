using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.Scanner;

namespace Credfeto.Gallery.Repository.Emitters
{
    public sealed class RawFileInfoEmitter : IFileEmitter
    {
        private readonly ConcurrentBag<Photo> _photos = new ConcurrentBag<Photo>();

        public Photo[] Photos => this.OrderedPhotos();

        public async Task FileFoundAsync(FileEntry entry)
        {
            string basePath = Path.Combine(path1: entry.RelativeFolder, Path.GetFileNameWithoutExtension(entry.LocalFileName));

            Photo item = await CreatePhotoRecordAsync(entry: entry, basePath: basePath);

            this.Store(item);
        }

        private Photo[] OrderedPhotos()
        {
            return this._photos.OrderBy(keySelector: x => x.UrlSafePath)
                       .ToArray();
        }

        private void Store(Photo photo)
        {
            this._photos.Add(photo);
        }

        private static async Task<Photo> CreatePhotoRecordAsync(FileEntry entry, string basePath)
        {
            string urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            List<ComponentFile> componentFiles = new List<ComponentFile>();

            TaskFactory<ComponentFile> factory = Task<ComponentFile>.Factory;

            Task<ComponentFile>[] tasks = entry.AlternateFileNames.Concat(new[] {entry.LocalFileName})
                                               .Select(selector: fileName => ReadComponentFileAsync(factory: factory, Path.Combine(path1: entry.Folder, path2: fileName)))
                                               .ToArray();

            Photo item = new Photo
                         {
                             BasePath = basePath,
                             UrlSafePath = urlSafePath,
                             PathHash = Hasher.HashBytes(Encoding.UTF8.GetBytes(urlSafePath)),
                             ImageExtension = Path.GetExtension(entry.LocalFileName),
                             Files = componentFiles
                         };

            ComponentFile[] results = await Task.WhenAll(tasks);

            componentFiles.AddRange(results);

            return item;
        }

        private static Task<ComponentFile> ReadComponentFileAsync(TaskFactory<ComponentFile> factory, string fileName)
        {
            return factory.StartNew(function: () => ReadComponentFile(fileName));
        }

        private static ComponentFile ReadComponentFile(string fileName)
        {
            FileInfo info = new FileInfo(fileName);
            string extension = info.Extension.ToLowerInvariant();

            return new ComponentFile {Extension = extension, Hash = string.Empty, LastModified = info.LastWriteTimeUtc, FileSize = info.Length};
        }
    }
}