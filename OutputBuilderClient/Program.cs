using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static int Main()
        {
            try
            {
                ProcessGallery();

                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: {0}", exception.Message);
                return 1;
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
                foreach (Photo sourcePhoto in GetAll(outputSession))
                {
                    if (liveItems.Contains(sourcePhoto.PathHash))
                    {
                        continue;
                    }

                    using (IDocumentSession deletionSession = documentStoreOutput.OpenSession())
                    {
                        deletionSession.Delete(deletionSession);

                        deletionSession.SaveChanges();
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

                foreach (Photo sourcePhoto in GetAll(inputSession))
                {
                    try
                    {
                        using (IDocumentSession outputSession = documentStoreOutput.OpenSession())
                        {
                            var targetPhoto = outputSession.Load<Photo>(sourcePhoto.PathHash);
                            bool rebuild = targetPhoto == null || HaveFilesChanged(sourcePhoto, targetPhoto);

                            if (rebuild)
                            {
                                Console.WriteLine("Rebuild: {0}", sourcePhoto.UrlSafePath);

                                foreach (
                                    ComponentFile file in
                                        sourcePhoto.Files.Where(s => string.IsNullOrWhiteSpace(s.Hash)))
                                {
                                    string filename = Path.Combine(Settings.Default.RootFolder,
                                                                   sourcePhoto.BasePath + file.Extension);

                                    file.Hash = Hasher.HashFile(filename);
                                }

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
                                Console.WriteLine("Unchanged: {0}", targetPhoto.UrlSafePath);
                            }
                        }

                        items.Add(sourcePhoto.PathHash);
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine("ERROR: Skipping image {0} due to exception {1}", sourcePhoto.UrlSafePath,
                                          exception.Message);
                    }
                }

                return items;
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

        private void SyncDetachedObjectToSession<T>(T sessionObject, T
                                                                         detachedObject)
        {
            PropertyInfo[] properties = typeof (T).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                object propertyValue =
                    property.GetValue(detachedObject, null);
                property.SetValue(sessionObject, propertyValue, null);
            }
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

                    if (componentFile.LastModified != found.LastModified)
                    {
                        return true;
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