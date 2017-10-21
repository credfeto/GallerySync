using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace BuildSiteIndex
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

        public void FileFound(FileEntry entry)
        {
            var fullPath = Path.Combine(_basePath, entry.RelativeFolder, entry.LocalFileName);

            var bytes = File.ReadAllBytes(fullPath);


            var photo = JsonConvert.DeserializeObject<Photo>(Encoding.UTF8.GetString(bytes));

            _photos.Add(photo);
        }
    }
}