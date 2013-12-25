using System;
using System.Collections.Generic;
using System.Linq;
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
            var contents = new Dictionary<string, GalleryEntry>();

            string dbInputFolder = Settings.Default.DatabaseInputFolder;

            var documentStoreInput = new EmbeddableDocumentStore
                {
                    DataDirectory = dbInputFolder
                };

            documentStoreInput.Initialize();


            AppendRootEntry(contents);

            const string albumsRoot = "albums";
            const string albumsTitle = "Albums";


            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                foreach (Photo sourcePhoto in GetAll(inputSession))
                {
                    string path = albumsRoot + "/" + sourcePhoto.UrlSafePath;
                    string breadcrumbs = albumsTitle + "\\" + sourcePhoto.BasePath;
                    Console.WriteLine("Item: {0}", path);

                    string[] pathFragments = path.Split('/');
                    string[] breadcrumbFragments = breadcrumbs.Split('\\');

                    EnsureParentFoldersExist(pathFragments, breadcrumbFragments, contents);

                    string parentLevel = "/" + string.Join("/", pathFragments.Take(pathFragments.Length - 1));


                    AppendPhotoEntry(contents, parentLevel, path, breadcrumbFragments[breadcrumbFragments.Length - 1], sourcePhoto);
                }
            }

            Console.WriteLine("Found {0} items total", contents.Count);
        }

        private static void AppendPhotoEntry(Dictionary<string, GalleryEntry> contents, string parentLevel, string path, string title, Photo sourcePhoto)
        {
            // TODO: Extract dates out of the metadata
            var dateCreated = sourcePhoto.Files.Min(file => file.LastModified);
            var dateUpdated = sourcePhoto.Files.Min(file => file.LastModified);

            //var taken = sourcePhoto.Metadata.FirstOrDefault(item => item.Name == MetadataNames.DateTaken);
            //if (taken != null)
            //{
            //    // Extract the date from the value;
            //}

            AppendEntry(contents, parentLevel, "/" + path, new GalleryEntry
                {
                    Title = title,
                    Children = new List<GalleryEntry>(),
                    DateCreated = dateCreated,
                    DateUpdated = dateUpdated
                });
        }

        private static void EnsureParentFoldersExist(string[] pathFragments, string[] breadcrumbFragments,
                                                     Dictionary<string, GalleryEntry> contents)
        {
            for (int folderLevel = 1; folderLevel < pathFragments.Length; ++folderLevel)
            {
                string level = "/" + string.Join("/", pathFragments.Take(folderLevel));

                GalleryEntry item;
                if (!contents.TryGetValue(level, out item))
                {
                    string parentLevel = "/" + string.Join("/", pathFragments.Take(folderLevel - 1));

                    AppendEntry(contents, parentLevel, level, new GalleryEntry
                        {
                            Title = breadcrumbFragments[folderLevel - 1],
                            Children = new List<GalleryEntry>(),
                            DateCreated = DateTime.MaxValue,
                            DateUpdated = DateTime.MinValue
                        });
                }
            }
        }

        private static void AppendEntry(Dictionary<string, GalleryEntry> contents, string parentPath, string itemPath,
                                        GalleryEntry entry)
        {
            GalleryEntry parent;
            if (!contents.TryGetValue(parentPath, out parent))
            {
                throw new ApplicationException("Could not find: " + parentPath);
            }

            Console.WriteLine(" * Path: {0}", itemPath);
            Console.WriteLine("   + Title: {0}", entry.Title);
            parent.Children.Add(entry);

            contents.Add(itemPath, entry);
        }

        private static void AppendRootEntry(Dictionary<string, GalleryEntry> contents)
        {
            var entry = new GalleryEntry
                {
                    Title = "Mark's Photos",
                    Children = new List<GalleryEntry>(),
                    DateCreated = DateTime.MaxValue,
                    DateUpdated = DateTime.MinValue
                };


            contents.Add("/", entry);
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