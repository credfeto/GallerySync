﻿using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ObjectModel;
using Scanner;
using StorageHelpers;

namespace BuildSiteIndex
{
    public class PhotoInfoEmitter : IFileEmitter
    {
        private readonly string _basePath;
        private readonly ConcurrentBag<Photo> _photos = new ConcurrentBag<Photo>();

        public PhotoInfoEmitter(string basePath)
        {
            this._basePath = basePath;
        }

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
            string fullPath = Path.Combine(this._basePath, entry.RelativeFolder, entry.LocalFileName);

            byte[] bytes = await FileHelpers.ReadAllBytes(fullPath);

            Photo photo = JsonConvert.DeserializeObject<Photo>(Encoding.UTF8.GetString(bytes));

            this._photos.Add(photo);
        }
    }
}