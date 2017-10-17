using System;
using System.Collections.Generic;
using System.Text;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    public class PhotoInfoEmitter : IFileEmitter
    {
        private readonly string _basePath;
        private readonly List<Photo> _photos = new List<Photo>();

        public PhotoInfoEmitter(string basePath)
        {
            _basePath = basePath;
        }
        
        public List<Photo> Photos
        {
            get { return _photos; }
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