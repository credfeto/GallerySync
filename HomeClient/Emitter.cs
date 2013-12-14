using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FileNaming;
using Raven.Client.Embedded;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace HomeClient
{
    public sealed class Emitter : IFileEmitter
    {
        private readonly EmbeddableDocumentStore _documentStore;

        public Emitter(EmbeddableDocumentStore documentStore)
        {
            _documentStore = documentStore;
        }

        public void FileFound(FileEntry entry)
        {            
            string basePath = Path.Combine(entry.RelativeFolder, Path.GetFileNameWithoutExtension(entry.LocalFileName));

            Photo item = CreatePhotoRecord(entry, basePath);

            Store(item);
        }

        private void Store(Photo photo)
        {
            using (var session = _documentStore.OpenSession())
            {
                session.Store(photo, photo.PathHash);
                
                session.SaveChanges();
            }
        }

        private Photo CreatePhotoRecord(FileEntry entry, string basePath)
        {
            string urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            var item = new Photo
                {
                    BasePath = basePath,
                    UrlSafePath = urlSafePath,
                    PathHash = Hasher.HashBytes(Encoding.UTF8.GetBytes(urlSafePath)),
                    ImageExtension = Path.GetExtension(entry.LocalFileName), Files = new List<ComponentFile>()
                    
                };

            AppendComponentFile(item, Path.Combine(entry.Folder, entry.LocalFileName));

            foreach (string fileName in entry.AlternateFileNames)
            {
                AppendComponentFile(item, Path.Combine(entry.Folder, fileName));
            }

            Console.WriteLine("Found: {0}", basePath);

            return item;
        }

        private void AppendComponentFile(Photo item, string fileName)
        {
            string extension = Path.GetExtension(fileName);

            var file = new ComponentFile
                {
                    Extension = extension,
                    Hash = Hasher.HashFile(fileName)
                };

            item.Files.Add(file);
        }
    }
}