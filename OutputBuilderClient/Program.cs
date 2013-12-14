using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            Process(documentStoreInput, documentStoreOutput);
        }

        private static void Process(EmbeddableDocumentStore documentStoreInput,
                                    EmbeddableDocumentStore documentStoreOutput)
        {
            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                foreach (Photo sourcePhoto in GetAll(inputSession))
                {
                    Console.WriteLine("{0}", sourcePhoto.UrlSafePath);

                    using (IDocumentSession outputSession = documentStoreOutput.OpenSession())
                    {
                        var targetPhoto = outputSession.Load<Photo>(sourcePhoto.PathHash);
                        bool rebuild = targetPhoto == null || HaveFilesChanged(sourcePhoto, targetPhoto);
                        if (rebuild)
                        {
                            Console.WriteLine("Rebuild: {0}", sourcePhoto.UrlSafePath);

                            outputSession.Store(sourcePhoto, sourcePhoto.PathHash);
                            outputSession.SaveChanges();
                        }
                        else
                        {
                            Console.WriteLine("Unchanged: {0}", targetPhoto.UrlSafePath);
                        }
                    }
                }
            }
        }

        private static bool HaveFilesChanged(Photo sourcePhoto, Photo targetPhoto)
        {
            if (sourcePhoto.Files.Count != targetPhoto.Files.Count)
            {
                return true;
            }

            foreach (var componentFile in targetPhoto.Files)
            {
                ComponentFile found = sourcePhoto.Files.FirstOrDefault( candiate => StringComparer.InvariantCultureIgnoreCase.Equals(candiate.Extension, componentFile.Extension) );
                
                if (found != null)
                {
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