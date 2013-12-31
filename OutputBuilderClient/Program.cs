using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FileNaming;
using OutputBuilderClient.Properties;
using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Client.Embedded;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    internal class Program
    {
        private static readonly object _lock = new object();

        private static int Main()
        {
            BoostPriority();

            try
            {
                ProcessGallery();

                return 0;
            }
            catch (Exception exception)
            {
                OutputText("Error: {0}", exception.Message);
                return 1;
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
            if (!Directory.Exists(dbOutputFolder))
            {
                Directory.CreateDirectory(dbOutputFolder);
            }

            var documentStoreOutput = new EmbeddableDocumentStore
                {
                    DataDirectory = dbOutputFolder
                };

            documentStoreOutput.Initialize();

            HashSet<string> liveItems = Process(documentStoreInput, documentStoreOutput);

            KillDeadItems(documentStoreOutput, liveItems);
        }

        private static void KillDeadItems(EmbeddableDocumentStore documentStoreOutput, HashSet<string> liveItems)
        {
            using (IDocumentSession outputSession = documentStoreOutput.OpenSession())
            {
                foreach (Photo sourcePhoto in GetAll(outputSession).AsParallel())
                {
                    if (liveItems.Contains(sourcePhoto.PathHash))
                    {
                        continue;
                    }

                    using (IDocumentSession deletionSession = documentStoreOutput.OpenSession())
                    {
                        var targetPhoto = outputSession.Load<Photo>(sourcePhoto.PathHash);
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
            }
        }

        private static HashSet<string> Process(EmbeddableDocumentStore documentStoreInput,
                                               EmbeddableDocumentStore documentStoreOutput)
        {
            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                var items = new HashSet<string>();

                foreach (Photo sourcePhoto in GetAll(inputSession).AsParallel())
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

                    if (build || rebuild)
                    {
                        OutputText(rebuild ? "Rebuild: {0}" : "Build: {0}", sourcePhoto.UrlSafePath);

                        UpdateFileHashes(targetPhoto, sourcePhoto);

                        sourcePhoto.Metadata = MetadataExtraction.ExtractMetadata(sourcePhoto);

                        sourcePhoto.ImageSizes = ImageExtraction.BuildImages(sourcePhoto);

                        if (targetPhoto != null)
                        {
                            UpdateTargetWithSourceProperties(targetPhoto, sourcePhoto);

                            outputSession.Store(targetPhoto, targetPhoto.PathHash);
                        }
                        else
                        {
                            outputSession.Store(sourcePhoto, sourcePhoto.PathHash);
                        }

                        outputSession.SaveChanges();
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
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<Photo> GetAll(IDocumentSession session)
        {
            using (
                IEnumerator<StreamResult<Photo>> enumerator = session.Advanced.Stream<Photo>(fromEtag: Etag.Empty,
                                                                                             start: 0,
                                                                                             pageSize: int.MaxValue))
                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current.Document;
                }
        }
    }
}