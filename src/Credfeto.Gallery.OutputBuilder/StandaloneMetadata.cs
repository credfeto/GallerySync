using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Credfeto.Gallery.Scanner;
using Microsoft.Extensions.Logging;

namespace Credfeto.Gallery.OutputBuilder
{
    internal static class StandaloneMetadata
    {
        public static IReadOnlyList<PhotoMetadata> ReadMetadata(string filename, ISettings settings, ILogger logging)
        {
            string folder = Path.GetDirectoryName(filename);
            string file = Path.GetFileName(filename);
            string extension = Path.GetExtension(filename);

            List<string> fileGroup = new List<string>();

            if (File.Exists(filename.Replace(extension, newValue: ".xmp")))
            {
                fileGroup.Add(file.Replace(extension, newValue: ".xmp"));
            }

            FileEntry entry = new FileEntry
                              {
                                  Folder = folder, RelativeFolder = folder.Substring(settings.RootFolder.Length + 1), LocalFileName = file, AlternateFileNames = fileGroup
                              };

            string basePath = Path.Combine(entry.RelativeFolder, Path.GetFileNameWithoutExtension(entry.LocalFileName));

            string urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            Photo photo = new Photo
                          {
                              BasePath = basePath,
                              UrlSafePath = urlSafePath,
                              PathHash = Hasher.HashBytes(Encoding.UTF8.GetBytes(urlSafePath)),
                              ImageExtension = Path.GetExtension(entry.LocalFileName),
                              Files = fileGroup.Select(selector: x => new ComponentFile
                                                                      {
                                                                          Extension = Path.GetExtension(x)
                                                                                          .TrimStart(trimChar: '.'),
                                                                          Hash = string.Empty,
                                                                          LastModified = new DateTime(year: 2014, month: 1, day: 1),
                                                                          FileSize = 1000
                                                                      })
                                               .ToList()
                          };

            List<PhotoMetadata> metadata = MetadataExtraction.ExtractMetadata(photo, settings);

            foreach (PhotoMetadata item in metadata)
            {
                logging.LogInformation($"{item.Name} = {item.Value}");
            }

            return metadata;
        }
    }
}