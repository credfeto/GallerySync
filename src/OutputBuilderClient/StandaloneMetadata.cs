using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileNaming;
using ObjectModel;
using Scanner;

namespace OutputBuilderClient
{
    internal static class StandaloneMetadata
    {
        public static async Task<List<PhotoMetadata>> ReadMetadata(string filename)
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
                                  Folder = folder, RelativeFolder = folder.Substring(Settings.RootFolder.Length + 1), LocalFileName = file, AlternateFileNames = fileGroup
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

            List<PhotoMetadata> metadata = MetadataExtraction.ExtractMetadata(photo);

            await Task.WhenAll(metadata.Select(selector: item => ConsoleOutput.Line(formatString: "{0} = {1}", item.Name, item.Value)));

            return metadata;
        }
    }
}