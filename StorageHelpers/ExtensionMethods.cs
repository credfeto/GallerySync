namespace StorageHelpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;

//    using Raven.Abstractions.Data;
//    using Raven.Abstractions.Smuggler;
//    using Raven.Client;
//    using Raven.Client.Embedded;
//    using Raven.Database.Smuggler;

    public static class ExtensionMethods
    {
//        public static void Backup(this EmbeddableDocumentStore documentStore, string path)
//        {
//            Directory.CreateDirectory(path);
//
//            string file = Path.Combine(path, "ravendb.backup");
//            RotateLastGenerations(file);
//
//            var options = new SmugglerDatabaseOptions();
//
//            var dumper = new DatabaseDataDumper(documentStore.DocumentDatabase, options);
//
//            var exportOptions = new SmugglerExportOptions<RavenConnectionStringOptions>() { ToFile = file };
//
//            dumper.ExportData(exportOptions);
//        }

//        public static IEnumerable<TObjectType> GetAll<TObjectType>(this IDocumentSession session)
//            where TObjectType : class
//        {
//            using (
//                IEnumerator<StreamResult<object>> enumerator = session.Advanced.Stream<object>(
//                    fromEtag: Etag.Empty,
//                    start: 0,
//                    pageSize: int.MaxValue))
//                while (enumerator.MoveNext())
//                {
//                    var file = enumerator.Current.Document as TObjectType;
//                    if (file != null)
//                    {
//                        yield return file;
//                    }
//                }
//        }

//        public static bool Restore(this EmbeddableDocumentStore documentStore, string path)
//        {
//            if (!Directory.Exists(path))
//            {
//                return false;
//            }
//
//            string file = Path.Combine(path, "ravendb.backup");
//            if (!File.Exists(file))
//            {
//                return false;
//            }
//
//            var options = new SmugglerDatabaseOptions();
//
//            var dumper = new DatabaseDataDumper(documentStore.DocumentDatabase, options);
//
//            var importOptions = new SmugglerImportOptions<RavenConnectionStringOptions>() { FromFile = file };
//            dumper.ImportData(importOptions);
//
//            return true;
//        }

        public static void RotateLastGenerations(string file)
        {
            FileHelpers.DeleteFile(file + ".9");
            RotateWithRetry(file + ".8", file + ".9");
            RotateWithRetry(file + ".7", file + ".8");
            RotateWithRetry(file + ".6", file + ".7");
            RotateWithRetry(file + ".5", file + ".6");
            RotateWithRetry(file + ".4", file + ".5");
            RotateWithRetry(file + ".3", file + ".4");
            RotateWithRetry(file + ".2", file + ".3");
            RotateWithRetry(file + ".1", file + ".2");
            RotateWithRetry(file + ".0", file + ".1");
            RotateWithRetry(file, file + ".1");
        }

        

        private static bool Rotate(string current, string previous)
        {
            Console.WriteLine("Moving {0} to {1}", current, previous);
            if (!File.Exists(current))
            {
                return true;
            }

            FileHelpers.DeleteFile(previous);

            try
            {
                File.Move(current, previous);
                return true;
            }
            catch (Exception exception)
            {
                Console.WriteLine("ERROR: Failed to move file (FAST): {0}", exception.Message);
                return SlowMove(current, previous);
            }
        }

        private static void RotateWithRetry(string current, string previous)
        {
            const int maxRetries = 5;

            for (int retry = 0; retry < maxRetries; ++retry)
            {
                if (Rotate(current, previous))
                {
                    return;
                }
            }
        }

        private static bool SlowMove(string current, string previous)
        {
            try
            {
                File.Copy(current, previous);
                File.Delete(current);
                return true;
            }
            catch (Exception exception)
            {
                Console.WriteLine("ERROR: Failed to move file (SLOW): {0}", exception.Message);
                return false;
            }
        }
    }
}