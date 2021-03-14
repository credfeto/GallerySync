using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.Scanner;

namespace Credfeto.Gallery.Repository.Emitters
{
    public class PhotoInfoEmitter : IFileEmitter
    {
        private readonly string _basePath;
        private readonly ConcurrentBag<Photo> _photos = new();

        public PhotoInfoEmitter(string basePath)
        {
            this._basePath = basePath;
        }

        public Photo[] Photos => this.OrderedPhotos();

        public async Task FileFoundAsync(FileEntry entry)
        {
            string fullPath = Path.Combine(path1: this._basePath, path2: entry.RelativeFolder, path3: entry.LocalFileName);

            Photo photo = await PhotoMetadataRepository.LoadAsync(fullPath);

            this._photos.Add(photo);
        }

        private Photo[] OrderedPhotos()
        {
            return this._photos.OrderBy(keySelector: x => x.UrlSafePath)
                       .ToArray();
        }
    }
}