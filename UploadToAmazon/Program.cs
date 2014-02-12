using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using FileNaming;
using Raven.Client;
using Raven.Client.Embedded;
using StorageHelpers;
using Twaddle.Gallery.ObjectModel;
using UploadToAmazon.Properties;

namespace UploadToAmazon
{
    internal class Program
    {
        public static int Main(string[] args)
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

        private static void ProcessGallery()
        {
            string sourceFolder = Settings.Default.SourceImagesFolder;
            string outputFolder = Settings.Default.OutputFolder;

            string dbInputFolder = Settings.Default.DatabaseInputFolder;

            var documentStoreInput = new EmbeddableDocumentStore
                {
                    DataDirectory = dbInputFolder
                };

            documentStoreInput.Initialize();

            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                foreach (FileToUpload fileToUpload in inputSession.GetAll<FileToUpload>())
                {
                    if (fileToUpload.Completed)
                    {
                        continue;
                    }

                    Console.WriteLine("Uploading: {0}", fileToUpload.FileName);
                    try
                    {
                        string source = Path.Combine(sourceFolder, fileToUpload.FileName);
                        string target = Path.Combine(outputFolder, fileToUpload.FileName);
                        string targetDir = Path.GetDirectoryName(target);
                        Directory.CreateDirectory(targetDir);
                        File.Copy(source, target, true);
                        fileToUpload.Completed = true;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine("ERROR: Failed to copy file - {0}", exception.Message);
                    }

                    if (fileToUpload.Completed)
                    {
                        string key = "U" + Hasher.HashBytes(Encoding.UTF8.GetBytes(fileToUpload.FileName));

                        using (IDocumentSession outputSession = documentStoreInput.OpenSession())
                        {
                            var changed = outputSession.Load<FileToUpload>(key);
                            if (changed != null)
                            {
                                changed.Completed = true;
                                outputSession.Store(changed, key);
                                outputSession.SaveChanges();
                            }

                        }
                    }
                }
            }
        }


        private static void BoostPriority()
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass =
                    ProcessPriorityClass.High;
            }
            catch (Exception)
            {
            }
        }
    }
}