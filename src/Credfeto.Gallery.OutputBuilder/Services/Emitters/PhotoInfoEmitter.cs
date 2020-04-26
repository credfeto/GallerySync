using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ObjectModel;
using Scanner;
using StorageHelpers;

namespace OutputBuilderClient.Services.Emitters
{
    public class PhotoInfoEmitter : IFileEmitter
    {
        private readonly string _basePath;
        private readonly ConcurrentBag<Photo> _photos = new ConcurrentBag<Photo>();

        public PhotoInfoEmitter(string basePath)
        {
            this._basePath = basePath;
        }

        public Photo[] Photos => this.OrderedPhotos();

        public async Task FileFoundAsync(FileEntry entry)
        {
            string fullPath = Path.Combine(this._basePath, entry.RelativeFolder, entry.LocalFileName);

            byte[] bytes = await FileHelpers.ReadAllBytesAsync(fullPath);

            Photo photo = JsonSerializer.Deserialize<Photo>(Encoding.UTF8.GetString(bytes));

            this._photos.Add(photo);
        }

        private Photo[] OrderedPhotos()
        {
            return this._photos.OrderBy(keySelector: x => x.UrlSafePath)
                       .ToArray();
        }
    }
}