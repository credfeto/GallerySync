using System;
using System.Collections.Generic;
using System.IO;
using Raven.Abstractions.Data;
using Raven.Abstractions.Smuggler;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Database.Smuggler;

namespace StorageHelpers
{
    public static class ExtensionMethods
    {
        public static IEnumerable<TObjectType> GetAll<TObjectType>(this IDocumentSession session)
            where TObjectType : class
        {
            using (
                IEnumerator<StreamResult<object>> enumerator =
                    session.Advanced.Stream<object>(fromEtag: Etag.Empty,
                                                    start: 0,
                                                    pageSize: Int32.MaxValue))
                while (enumerator.MoveNext())
                {
                    var file = enumerator.Current.Document as TObjectType;
                    if (file != null)
                    {
                        yield return file;
                    }
                }
        }


        public static void Backup(this EmbeddableDocumentStore documentStore, string path)
        {
            Directory.CreateDirectory(path);

            string file = Path.Combine(path, "ravendb.backup");            
            Rotate(file + ".8", file + ".9");
            Rotate(file + ".7", file + ".8");
            Rotate(file + ".6", file + ".7");
            Rotate(file + ".5", file + ".6");
            Rotate(file + ".4", file + ".5");
            Rotate(file + ".3", file + ".4");
            Rotate(file + ".2", file + ".3");
            Rotate(file + ".1", file + ".2");
            Rotate(file + ".0", file + ".1");
            Rotate(file, file + ".1");            

            var options = new SmugglerOptions
                {
                    BackupPath = file
                };

            var dumper = new DataDumper(documentStore.DocumentDatabase, options);

            dumper.ExportData(null, options, false);
        }

        private static void Rotate(string current, string previous)
        {
            if (!File.Exists(current))
            {
                return;                
            }

            if (File.Exists(previous))
            {
                File.Delete(previous);
            }

            File.Move(current, previous);
        }

        public static void Restore(this EmbeddableDocumentStore documentStore, string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            string file = Path.Combine(path, "ravendb.backup");
            if (!File.Exists(file))
            {
                return;
            }

            var options = new SmugglerOptions
                {
                    BackupPath = file
                };

            var dumper = new DataDumper(documentStore.DocumentDatabase, options);

            dumper.ImportData(options);
        }
    }
}