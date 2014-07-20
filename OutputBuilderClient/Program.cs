using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FileNaming;
using OutputBuilderClient.Properties;
using Raven.Client;
using Raven.Client.Embedded;
using StorageHelpers;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    internal class Program
    {
        private static readonly object _lock = new object();

        private static int Main(string[] args)
        {
            if (args.Length == 1)
            {
                try
                {
                    ReadMetadata(args[0]);

                    return 0;
                }
                catch (Exception exception)
                {
                    OutputText("Error: {0}", exception.Message);
                    OutputText("Stack Trace: {0}", exception.StackTrace);
                    return 1;
                }
            }

            BoostPriority();

            try
            {
                ProcessGallery();

                return 0;
            }
            catch (Exception exception)
            {
                OutputText("Error: {0}", exception.Message);
                OutputText("Stack Trace: {0}", exception.StackTrace);
                return 1;
            }
        }

        private static void ReadMetadata(string filename)
        {
            string folder = Path.GetDirectoryName(filename);
            string file = Path.GetFileName(filename);
            string extension = Path.GetExtension(filename);

            var fileGroup = new List<string>();
            if (File.Exists(filename.Replace(extension, ".xmp")))
            {
                fileGroup.Add(file.Replace(extension, ".xmp"));
            }


            var entry = new FileEntry
                {
                    Folder = folder,
                    RelativeFolder = folder.Substring(Settings.Default.RootFolder.Length + 1),
                    LocalFileName = file,
                    AlternateFileNames = fileGroup
                };


            string basePath = Path.Combine(entry.RelativeFolder, Path.GetFileNameWithoutExtension(entry.LocalFileName));

            string urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            var photo = new Photo
                {
                    BasePath = basePath,
                    UrlSafePath = urlSafePath,
                    PathHash = Hasher.HashBytes(Encoding.UTF8.GetBytes(urlSafePath)),
                    ImageExtension = Path.GetExtension(entry.LocalFileName),
                    Files = fileGroup.Select(x =>
                                             new ComponentFile
                                                 {
                                                     Extension = Path.GetExtension(x).TrimStart('.'),
                                                     Hash = string.Empty,
                                                     LastModified = new DateTime(2014, 1, 1),
                                                     FileSize = 1000
                                                 }
                        ).ToList()
                };

            List<PhotoMetadata> metadata = MetadataExtraction.ExtractMetadata(photo);
            foreach (PhotoMetadata item in metadata)
            {
                Console.WriteLine("{0} = {1}", item.Name, item.Value);
            }
        }

        private static void BoostPriority()
        {
            try
            {
                System.Diagnostics.Process.GetCurrentProcess().PriorityClass =
                    ProcessPriorityClass.High;
            }
            catch (Exception)
            {
            }
        }

        private static void ProcessGallery()
        {
            string dbInputFolder = Settings.Default.DatabaseInputFolder;

            var documentStoreInput = new EmbeddableDocumentStore
                {
                    DataDirectory = dbInputFolder
                };

            documentStoreInput.Initialize();

            string dbOutputFolder = Settings.Default.DatabaseOutputFolder;
            bool restore = !Directory.Exists(dbOutputFolder) && Directory.Exists(Settings.Default.DatabaseBackupFolder);
            if (!Directory.Exists(dbOutputFolder))
            {
                Directory.CreateDirectory(dbOutputFolder);
            }

            var documentStoreOutput = new EmbeddableDocumentStore
                {
                    DataDirectory = dbOutputFolder
                };

            documentStoreOutput.Initialize();

            if (restore)
            {
                documentStoreOutput.Restore(Settings.Default.DatabaseBackupFolder);
            }

            HashSet<string> liveItems = Process(documentStoreInput, documentStoreOutput);

            KillDeadItems(documentStoreOutput, liveItems);

            documentStoreOutput.Backup(Settings.Default.DatabaseBackupFolder);
        }

        private static void KillDeadItems(EmbeddableDocumentStore documentStoreOutput, HashSet<string> liveItems)
        {
            using (IDocumentSession outputSession = documentStoreOutput.OpenSession())
            {
                foreach (Photo sourcePhoto in outputSession.GetAll<Photo>().AsParallel())
                {
                    if (liveItems.Contains(sourcePhoto.PathHash))
                    {
                        continue;
                    }

                    KillOnePhoto(documentStoreOutput, sourcePhoto);
                }
            }
        }

        private static void KillOnePhoto(EmbeddableDocumentStore documentStoreOutput, Photo sourcePhoto)
        {
            using (IDocumentSession deletionSession = documentStoreOutput.OpenSession())
            {
                var targetPhoto = deletionSession.Load<Photo>(sourcePhoto.PathHash);
                if (targetPhoto != null)
                {
                    OutputText("Deleting {0} as no longer exists", sourcePhoto.UrlSafePath);
                    deletionSession.Delete(targetPhoto);

                    deletionSession.SaveChanges();
                }
                else
                {
                    OutputText("Could not delete {0}", sourcePhoto.UrlSafePath);
                }
            }
        }

        private static HashSet<string> Process(EmbeddableDocumentStore documentStoreInput,
                                               EmbeddableDocumentStore documentStoreOutput)
        {
            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                var items = new HashSet<string>();

                foreach (Photo sourcePhoto in inputSession.GetAll<Photo>().AsParallel())
                {
                    ProcessSinglePhoto(documentStoreOutput, sourcePhoto, items);
                }

                return items;
            }
        }

        private static void ProcessSinglePhoto(EmbeddableDocumentStore documentStoreOutput, Photo sourcePhoto,
                                               HashSet<string> items)
        {
            try
            {
                using (IDocumentSession outputSession = documentStoreOutput.OpenSession())
                {
                    var targetPhoto = outputSession.Load<Photo>(sourcePhoto.PathHash);
                    bool build = targetPhoto == null;
                    bool rebuild = targetPhoto != null && HaveFilesChanged(sourcePhoto, targetPhoto);
                    bool rebuildMetadata = targetPhoto != null &&
                                           MetadataVerionOutOfDate(targetPhoto);

                    if (build || rebuild || rebuildMetadata)
                    {
                        ProcessOneFile(outputSession, sourcePhoto, targetPhoto, rebuild, rebuildMetadata);
                    }
                    else
                    {
                        OutputText("Unchanged: {0}", targetPhoto.UrlSafePath);
                    }
                }

                lock (_lock)
                {
                    items.Add(sourcePhoto.PathHash);
                }
            }
            catch (Exception exception)
            {
                OutputText("ERROR: Skipping image {0} due to exception {1}", sourcePhoto.UrlSafePath,
                           exception.Message);
                OutputText("Stack Trace: {0}", exception.StackTrace);
            }
        }

        private static bool MetadataVerionOutOfDate(Photo targetPhoto)
        {
            if (targetPhoto.Version <= Constants.CurrentMetadataVersion)
            {
                OutputText(" +++ Metadata update: Metadata version out of date. (Current: " + targetPhoto.Version + " Expected: " + Constants.CurrentMetadataVersion +")");
                return true;
            }

            return false;
        }

        private static void ProcessOneFile(IDocumentSession outputSession, Photo sourcePhoto, Photo targetPhoto, bool rebuild, bool rebuildMetadata)
        {
            OutputText(rebuild ? "Rebuild: {0}" : "Build: {0}", sourcePhoto.UrlSafePath);

            UpdateFileHashes(targetPhoto, sourcePhoto);

            var buildMetadata = targetPhoto == null || rebuild || rebuildMetadata || (targetPhoto != null && targetPhoto.Metadata == null );

            if (buildMetadata)
            {
                sourcePhoto.Metadata = MetadataExtraction.ExtractMetadata(sourcePhoto);
            }
            else
            {
                sourcePhoto.Metadata = targetPhoto.Metadata;
            }

            var buildImages = targetPhoto == null || rebuild || ( targetPhoto != null && !targetPhoto.ImageSizes.HasAny() );

            var filesCreated = new List<string>();            
            if (buildImages)
            {
                sourcePhoto.ImageSizes = ImageExtraction.BuildImages(sourcePhoto, filesCreated);
            }
            else
            {
                sourcePhoto.ImageSizes = targetPhoto.ImageSizes;                
            }

            sourcePhoto.Version = Constants.CurrentMetadataVersion;
            
            if (targetPhoto != null)
            {                
                UpdateTargetWithSourceProperties(targetPhoto, sourcePhoto);
                targetPhoto.Version = Constants.CurrentMetadataVersion;

                if (buildImages)
                {
                    AddUploadFiles(filesCreated, outputSession);
                }

                outputSession.Store(targetPhoto, targetPhoto.PathHash);
            }
            else
            {
                
                AddUploadFiles(filesCreated, outputSession);
                outputSession.Store(sourcePhoto, sourcePhoto.PathHash);
            }

            outputSession.SaveChanges();
        }


        private static void AddUploadFiles(List<string> filesCreated, IDocumentSession outputSession)
        {
            foreach (string file in filesCreated)
            {
                string key = "U" + Hasher.HashBytes(Encoding.UTF8.GetBytes(file));

                var existing = outputSession.Load<FileToUpload>(key);
                if (existing == null)
                {
                    var fileToUpload = new FileToUpload
                        {
                            FileName = file,
                            Completed = false
                        };

                    outputSession.Store(fileToUpload, key);
                }
                else
                {
                    if (existing.Completed)
                    {
                        existing.Completed = false;
                        outputSession.Store(existing, key);
                    }
                }
            }
        }

        private static void OutputText(string formatString, params object[] parameters)
        {
            string text = string.Format(formatString, parameters);

            lock (_lock)
            {
                Console.WriteLine(text);
            }
        }

        private static void UpdateFileHashes(Photo targetPhoto, Photo sourcePhoto)
        {
            if (targetPhoto != null)
            {
                foreach (
                    ComponentFile sourceFile in
                        sourcePhoto.Files.Where(s => string.IsNullOrWhiteSpace(s.Hash)))
                {
                    ComponentFile targetFile =
                        targetPhoto.Files.FirstOrDefault(s =>
                                                         s.Extension == sourceFile.Extension &&
                                                         !string.IsNullOrWhiteSpace(s.Hash));
                    if (targetFile != null)
                    {
                        sourceFile.Hash = targetFile.Hash;
                    }
                }
            }

            foreach (
                ComponentFile file in
                    sourcePhoto.Files.Where(s => string.IsNullOrWhiteSpace(s.Hash)))
            {
                string filename = Path.Combine(Settings.Default.RootFolder,
                                               sourcePhoto.BasePath + file.Extension);

                file.Hash = Hasher.HashFile(filename);
            }
        }

        private static void UpdateTargetWithSourceProperties(Photo targetPhoto, Photo sourcePhoto)
        {
            targetPhoto.Version = sourcePhoto.Version;
            targetPhoto.UrlSafePath = sourcePhoto.UrlSafePath;
            targetPhoto.BasePath = sourcePhoto.BasePath;
            targetPhoto.PathHash = sourcePhoto.PathHash;
            targetPhoto.ImageExtension = sourcePhoto.ImageExtension;
            targetPhoto.Files = sourcePhoto.Files;
            targetPhoto.Metadata = sourcePhoto.Metadata;
            targetPhoto.ImageSizes = sourcePhoto.ImageSizes;
        }

        private static bool HaveFilesChanged(Photo sourcePhoto, Photo targetPhoto)
        {
            if (sourcePhoto.Files.Count != targetPhoto.Files.Count)
            {
                OutputText(" +++ Metadata update: File count changed");
                return true;
            }

            foreach (ComponentFile componentFile in targetPhoto.Files)
            {
                ComponentFile found =
                    sourcePhoto.Files.FirstOrDefault(
                        candiate =>
                        StringComparer.InvariantCultureIgnoreCase.Equals(candiate.Extension, componentFile.Extension));

                if (found != null)
                {
                    if (componentFile.FileSize != found.FileSize)
                    {
                        OutputText(" +++ Metadata update: File size changed (File: " + found.Extension + ")");
                        return true;
                    }

                    if (componentFile.LastModified == found.LastModified)
                    {
                        // Assume if file modified date not changed then the file itself hasn't changed
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(found.Hash))
                    {
                        string filename = Path.Combine(Settings.Default.RootFolder,
                                                       sourcePhoto.BasePath + componentFile.Extension);

                        found.Hash = Hasher.HashFile(filename);
                    }

                    if (componentFile.Hash != found.Hash)
                    {
                        OutputText(" +++ Metadata update: File hash changed (File: " + found.Extension + ")");
                        return true;
                    }
                }
                else
                {
                    OutputText(" +++ Metadata update: File missing (File: " + componentFile.Extension + ")");
                    return true;
                }
            }

            return false;
        }
    }
}