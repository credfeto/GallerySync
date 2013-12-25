using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileNaming;
using Raven.Client;
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
            using (IDocumentSession session = _documentStore.OpenSession())
            {
                session.Store(photo, photo.PathHash);

                session.SaveChanges();
            }
        }

        private Photo CreatePhotoRecord(FileEntry entry, string basePath)
        {
            string urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            var componentFiles = new List<ComponentFile>();

            TaskFactory<ComponentFile> factory = Task<ComponentFile>.Factory;

            Task<ComponentFile>[] tasks =
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

            Task.WhenAll(tasks).ContinueWith(t => componentFiles.AddRange(t.Result)).Wait();

            Console.WriteLine("Found: {0}", basePath);

            return item;
        }

        private static Task<ComponentFile> ReadComponentFile(TaskFactory<ComponentFile> factory, string fileName)
        {
            return factory.StartNew(() => ReadComponentFIle2(fileName));
        }

        private static ComponentFile ReadComponentFIle2(string fileName)
        {
            var info = new FileInfo(fileName);
            string extension = info.Extension.ToLowerInvariant();

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