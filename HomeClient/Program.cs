using System;
using System.IO;
using HomeClient.Properties;
using Raven.Client.Embedded;
using Twaddle.Directory.Scanner;

namespace HomeClient
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
            string baseFolder = Settings.Default.RootFolder;

            string dbFolder = Settings.Default.DatabaseOutputFolder;
            if (Directory.Exists(dbFolder))
            {
                Directory.Delete(dbFolder, true);
            }

            Directory.CreateDirectory(dbFolder);

            var documentStore = new EmbeddableDocumentStore
                {
                    DataDirectory = dbFolder
                };

            documentStore.Initialize();

            var emitter = new Emitter(documentStore);

            long filesFound = DirectoryScanner.ScanFolder(baseFolder, emitter);

            Console.WriteLine("Files Found: {0}", filesFound);
        }
    }
}