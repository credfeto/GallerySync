using System;
using System.Collections.Generic;
using BuildSiteIndex.Properties;
using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Client.Embedded;
using Twaddle.Gallery.ObjectModel;

namespace BuildSiteIndex
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

            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                foreach (Photo sourcePhoto in GetAll(inputSession))
                {
                    Console.WriteLine(sourcePhoto.BasePath);
                }
            }
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