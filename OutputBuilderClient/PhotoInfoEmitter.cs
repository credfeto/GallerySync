using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ObjectModel;
using Scanner;
using StorageHelpers;

namespace OutputBuilderClient
{
    public class PhotoInfoEmitter : IFileEmitter
    {
        private readonly string _basePath;
        private readonly ConcurrentBag<Photo> _photos = new ConcurrentBag<Photo>();

        public PhotoInfoEmitter(string basePath)
        {
            _basePath = basePath;
        }

        public Photo[] Photos
        {
            get { return _photos.OrderBy(x => x.UrlSafePath).ToArray(); }
        }

        public async Task FileFound(FileEntry entry)
        {
            var fullPath = Path.Combine(_basePath, entry.RelativeFolder, entry.LocalFileName);

            var bytes = await FileHelpers.ReadAllBytes(fullPath);

            var photo = JsonConvert.DeserializeObject<Photo>(Encoding.UTF8.GetString(bytes));

            _photos.Add(photo);
        }
    }
}