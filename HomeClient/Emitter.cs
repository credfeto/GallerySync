using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Raven.Client.Embedded;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace HomeClient
{
    public sealed class Emitter : IFileEmitter
    {
        private const string Replacementchar = "-";

        private static readonly Regex AcceptableUrlCharacters = new Regex(@"[^\w\-/]",
                                                                          RegexOptions.Compiled |
                                                                          RegexOptions.CultureInvariant);

        private static readonly Regex NoRepeatingHyphens = new Regex(@"(\-{2,})",
                                                                     RegexOptions.Compiled |
                                                                     RegexOptions.CultureInvariant);

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
            string urlSafePath = BuildUrlSafePath(basePath);

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

        private static string BuildUrlSafePath(string basePath)
        {
            return
                NoRepeatingHyphens.Replace(
                    AcceptableUrlCharacters.Replace(basePath.Trim().Replace(@"\", @"/"), Replacementchar),
                    Replacementchar)
                                  .TrimEnd(Replacementchar.ToCharArray()).ToLowerInvariant();
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