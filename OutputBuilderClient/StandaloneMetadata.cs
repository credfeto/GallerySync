using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using FileNaming;
using OutputBuilderClient.Properties;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    internal static class StandaloneMetadata
    {
        public static async Task<List<PhotoMetadata>> ReadMetadata(string filename)
        {
            var folder = Path.GetDirectoryName(filename);
            var file = Path.GetFileName(filename);
            var extension = Path.GetExtension(filename);

            var fileGroup = new List<string>();
            if (File.Exists(filename.Replace(extension, ".xmp")))
                fileGroup.Add(file.Replace(extension, ".xmp"));

            var entry = new FileEntry
            {
                Folder = folder,
                RelativeFolder = folder.Substring(Settings.Default.RootFolder.Length + 1),
                LocalFileName = file,
                AlternateFileNames = fileGroup
            };

            var basePath = Path.Combine(
                entry.RelativeFolder,
                Path.GetFileNameWithoutExtension(entry.LocalFileName));

            var urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            var photo = new Photo
            {
                BasePath = basePath,
                UrlSafePath = urlSafePath,
                PathHash = Hasher.HashBytes(Encoding.UTF8.GetBytes(urlSafePath)),
                ImageExtension = Path.GetExtension(entry.LocalFileName),
                Files =
                    fileGroup.Select(
                        x =>
                            new ComponentFile
                            {
                                Extension =
                                    Path.GetExtension(x)
                                        .TrimStart('.'),
                                Hash = string.Empty,
                                LastModified = new DateTime(2014, 1, 1),
                                FileSize = 1000
                            }).ToList()
            };

            var metadata = MetadataExtraction.ExtractMetadata(photo);

            await Task.WhenAll(metadata.Select(
                item => ConsoleOutput.Line("{0} = {1}", item.Name, item.Value)
            ));

            return metadata;
        }
    }
}