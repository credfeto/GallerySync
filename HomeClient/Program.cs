using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HomeClient.Properties;
using Raven.Client.Embedded;
using Twaddle.Directory.Scanner;

namespace HomeClient
{
    internal class Program
    {
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
                Console.WriteLine("Error: {0}", exception.Message);
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

            var scores = new[]
                {
                    ".xmp",
                    ".jpg",
                    ".cr2",
                    ".mrw",
                    ".rw2",
                    ".tif",
                    ".tiff",
                    ".psd"
                };

            var sidecarFiles = new[]
                {
                    ".xmp"
                };

            long filesFound = DirectoryScanner.ScanFolder(baseFolder, emitter, scores.ToList(), sidecarFiles.ToList());

            Console.WriteLine("Files Found: {0}", filesFound);
        }
    }
}