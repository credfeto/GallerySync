using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using FileNaming;
using Raven.Abstractions.Smuggler;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Database;
using Raven.Database.Smuggler;
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

            Console.WriteLine("UploadToAmazon");

            try
            {
                ProcessGallery();                

                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: {0}", exception.Message);
                Console.WriteLine("Stack Trace: {0}", exception.StackTrace);
                return 1;
            }
        }

        private static void ProcessGallery()
        {
            string sourceFolder = Settings.Default.SourceImagesFolder;
            string outputFolder = Settings.Default.OutputFolder;

            string dbInputFolder = Settings.Default.DatabaseInputFolder;
            bool restore = !Directory.Exists(dbInputFolder) && Directory.Exists(Settings.Default.DatabaseBackupFolder);

            var documentStoreInput = new EmbeddableDocumentStore
                {
                    DataDirectory = dbInputFolder
                };

            documentStoreInput.Initialize();

            if (restore)
            {
                documentStoreInput.Restore(Settings.Default.DatabaseBackupFolder);
            }

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
                        if (File.Exists(source))
                        {

                            string target = Path.Combine(outputFolder, fileToUpload.FileName);
                            string targetDir = Path.GetDirectoryName(target);
                            Directory.CreateDirectory(targetDir);
                            File.Copy(source, target, true);
                            fileToUpload.Completed = true;
                        }
                        else
                        {
                            Console.WriteLine("+++ Source file does not exist");
                            fileToUpload.Completed = true;
                        }
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine("ERROR: Failed to copy file - {0}", exception.Message);
                        Console.WriteLine("Stack Trace: {0}", exception.StackTrace);
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

            documentStoreInput.Backup(Settings.Default.DatabaseBackupFolder);
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