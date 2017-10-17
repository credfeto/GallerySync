using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using FileNaming;
using Newtonsoft.Json;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace HomeClient
{
    public sealed class Emitter : IFileEmitter
    {
        private readonly string _dbFolder;

        public Emitter(string dbFolder)
        {
            _dbFolder = dbFolder;
            Directory.Delete(dbFolder, true);
        }

        public void FileFound(FileEntry entry)
        {
            var basePath = Path.Combine(entry.RelativeFolder, Path.GetFileNameWithoutExtension(entry.LocalFileName));

            var item = CreatePhotoRecord(entry, basePath);

            Store(item);
        }

        private void Store(Photo photo)
        {
            var safeUrl = photo.UrlSafePath.Replace('/', Path.DirectorySeparatorChar);
            safeUrl = safeUrl.TrimEnd(Path.DirectorySeparatorChar);
            safeUrl += ".info";

            var outputPath = Path.Combine(
                _dbFolder,
                safeUrl
            );

            var txt = JsonConvert.SerializeObject(photo);
            FileHelpers.WriteAllBytes(outputPath, Encoding.UTF8.GetBytes(txt));
        }

        private static Photo CreatePhotoRecord(FileEntry entry, string basePath)
        {
            var urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            var componentFiles = new List<ComponentFile>();

            var factory = Task<ComponentFile>.Factory;

            var tasks =
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
            var extension = info.Extension.ToLowerInvariant();

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