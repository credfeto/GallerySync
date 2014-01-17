using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BuildSiteIndex.Properties;
using FileNaming;
using Newtonsoft.Json;
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
                Process.GetCurrentProcess().PriorityClass =
                    ProcessPriorityClass.High;
            }
            catch (Exception)
            {
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
                    string path = EnsureTerminatedPath("/" + albumsRoot + "/" + sourcePhoto.UrlSafePath);
                    string breadcrumbs = EnsureTerminatedBreadcrumbs("\\" + albumsTitle + "\\" + sourcePhoto.BasePath);
                    Console.WriteLine("Item: {0}", path);

                    string[] pathFragments = path.Split('/').Where(IsNotEmpty).ToArray();
                    string[] breadcrumbFragments = breadcrumbs.Split('\\').Where(IsNotEmpty).ToArray();

                    EnsureParentFoldersExist(pathFragments, breadcrumbFragments, contents);

                    string parentLevel =
                        EnsureTerminatedPath("/" + string.Join("/", pathFragments.Take(pathFragments.Length - 1)));

                    string title = ExtractTitle(sourcePhoto);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        // fallback to a title based on the filename
                        title = breadcrumbFragments[breadcrumbFragments.Length - 1];
                    }

                    AppendPhotoEntry(contents, parentLevel, path,
                                     title,
                                     sourcePhoto);
                }
            }

            Console.WriteLine("Found {0} items total", contents.Count);

            ProduceJsonFile(contents);
        }

        private static string EnsureTerminatedPath(string path)
        {
            return EnsureEndsWithSpecificTerminator(path, "/");
        }

        private static string EnsureTerminatedBreadcrumbs(string path)
        {
            return EnsureEndsWithSpecificTerminator(path, "\\");
        }

        private static string EnsureEndsWithSpecificTerminator(string path, string terminator)
        {
            if (!path.EndsWith(terminator, StringComparison.Ordinal))
            {
                return path + terminator;
            }
            return path;
        }

        private static bool IsNotEmpty(string arg)
        {
            return !string.IsNullOrWhiteSpace(arg);
        }

        private static void ProduceJsonFile(Dictionary<string, GalleryEntry> contents)
        {            
            var data = new GallerySiteIndex
                {
                    version = 1,
                    items = (from parentRecord in contents.Values
                             orderby parentRecord.Path
                             let siblings = GetSiblings(contents, parentRecord)
                             let firstItem =
                                 GetFirstItem(siblings, parentRecord)
                             let lastItem =
                                 GetLastItem(siblings, parentRecord)
                             let previousItem =
                                 GetPreviousItem(siblings, parentRecord, firstItem)
                             let nextItem =
                                 GetNextItem(siblings, parentRecord, lastItem)
                             select new GalleryItem
                                 {
                                     Path = parentRecord.Path,
                                     Title = parentRecord.Title,
                                     Description = parentRecord.Description,
                                     DateCreated = parentRecord.DateCreated,
                                     DateUpdated = parentRecord.DateUpdated,
                                     Location = parentRecord.Location,
                                     Type = parentRecord.Children.Any() ? "folder" : "photo",
                                     ImageSizes = parentRecord.ImageSizes ?? new List<ImageSize>(),
                                     Metadata = parentRecord.Metadata ?? new List<PhotoMetadata>(),
                                     Keywords = parentRecord.Keywords ?? new List<string>(),
                                     First = firstItem,
                                     Previous = previousItem,
                                     Next = nextItem,
                                     Last = lastItem,
                                     Children = (from childRecord in parentRecord.Children
                                                 orderby childRecord.Path
                                                 select CreateGalleryChildItem(childRecord)).ToList(),
                                 }).ToList(),
                    deletedItems = new List<string>()
                };

            string outputFilename = Path.Combine(Settings.Default.OutputFolder, "site.js");

            string json = JsonConvert.SerializeObject(data);
            if (File.Exists(outputFilename))
            {
                Console.WriteLine("Previous Json file exists");
                var originalBytes = File.ReadAllBytes(outputFilename);
                var decoded = Encoding.UTF8.GetString(originalBytes);
                if (decoded == json)
                {
                    Console.WriteLine("No changes since last run");
                    return;
                }
            }

            byte[] encoded = Encoding.UTF8.GetBytes(json);
            File.WriteAllBytes(outputFilename, encoded);
        }

        private static GalleryChildItem GetNextItem(List<GalleryEntry> siblings, GalleryEntry parentRecord,
                                                    GalleryChildItem lastItem)
        {
            GalleryChildItem candidate = siblings.SkipWhile(x => x != parentRecord)
                                                 .Skip(1)
                                                 .Select(CreateGalleryChildItem).FirstOrDefault();

            return SkipKnownItem(candidate, lastItem);
        }

        private static GalleryChildItem SkipKnownItem(GalleryChildItem candidate,
                                                      GalleryChildItem itemToIgnoreIfMataches)
        {
            if (candidate != null && candidate.Path == itemToIgnoreIfMataches.Path)
            {
                return null;
            }

            return candidate;
        }

        private static GalleryChildItem GetFirstItem(List<GalleryEntry> siblings, GalleryEntry parentRecord)
        {
            GalleryChildItem candidate = siblings
                .Select(CreateGalleryChildItem).FirstOrDefault();

            return SkipKnownItem(candidate, parentRecord);
        }

        private static GalleryChildItem SkipKnownItem(GalleryChildItem candidate, GalleryEntry itemToIgnoreIfMataches)
        {
            if (candidate != null && candidate.Path == itemToIgnoreIfMataches.Path)
            {
                return null;
            }

            return candidate;
        }

        private static GalleryChildItem CreateGalleryChildItem(GalleryEntry firstRecord)
        {
            return new GalleryChildItem
                {
                    Path = firstRecord.Path,
                    Title = firstRecord.Title,
                    Description = firstRecord.Description,
                    DateCreated = firstRecord.DateCreated,
                    DateUpdated = firstRecord.DateUpdated,
                    Location = firstRecord.Location,
                    Type = firstRecord.Children.Any() ? "folder" : "photo",
                    ImageSizes = firstRecord.ImageSizes ?? new List<ImageSize>()
                };
        }

        private static GalleryChildItem GetLastItem(List<GalleryEntry> siblings, GalleryEntry parentRecord)
        {
            GalleryChildItem candidate = siblings
                .Select(CreateGalleryChildItem).LastOrDefault();

            return SkipKnownItem(candidate, parentRecord);
        }

        private static GalleryChildItem GetPreviousItem(List<GalleryEntry> siblings, GalleryEntry parentRecord,
                                                        GalleryChildItem firstItem)
        {
            GalleryChildItem candidate = siblings.TakeWhile(x => x != parentRecord)
                                                 .Select(CreateGalleryChildItem).LastOrDefault();

            return SkipKnownItem(candidate, firstItem);
        }

        public IEnumerable<Tuple<T, T, T>> WithNextAndPrevious<T>(IEnumerable<T> source)
        {
            // Actually yield "the previous two" as well as the current one - this
            // is easier to implement than "previous and next" but they're equivalent
            using (IEnumerator<T> iterator = source.GetEnumerator())
            {
                if (!iterator.MoveNext())
                {
                    yield break;
                }
                T lastButOne = iterator.Current;
                if (!iterator.MoveNext())
                {
                    yield break;
                }
                T previous = iterator.Current;
                while (iterator.MoveNext())
                {
                    T current = iterator.Current;
                    yield return Tuple.Create(lastButOne, previous, current);
                    lastButOne = previous;
                    previous = current;
                }
            }
        }


        private static List<GalleryEntry> GetSiblings(Dictionary<string, GalleryEntry> contents, GalleryEntry entry)
        {
            if (entry.Path.Length == 1)
            {
                return new List<GalleryEntry>();
            }

            int parentPathIndex = entry.Path.LastIndexOf('/', entry.Path.Length - 2);
            if (parentPathIndex == -1)
            {
                return new List<GalleryEntry>();
            }

            string parentPath = entry.Path.Substring(0, parentPathIndex + 1);

            GalleryEntry parentItem;
            if (contents.TryGetValue(parentPath, out parentItem) && parentItem != null)
            {
                return new List<GalleryEntry>(parentItem.Children.OrderBy(item => item.Path));
            }

            return new List<GalleryEntry>();
        }


        private static void AppendPhotoEntry(Dictionary<string, GalleryEntry> contents, string parentLevel, string path,
                                             string title, Photo sourcePhoto)
        {
            DateTime dateCreated;
            DateTime dateUpdated;
            ExtractDates(sourcePhoto, out dateCreated, out dateUpdated);

            string description = ExtractDescription(sourcePhoto);

            Location location = ExtractLocation(sourcePhoto);

            int rating = ExtractRating(sourcePhoto);

            List<string> keywords = ExtractKeywords(sourcePhoto);

            AppendEntry(contents, parentLevel, path, new GalleryEntry
                {
                    Path = path,
                    Title = title,
                    Description = description,
                    Children = new List<GalleryEntry>(),
                    Location = location,
                    ImageSizes = sourcePhoto.ImageSizes,
                    Rating = rating,
                    Metadata = sourcePhoto.Metadata.Where(IsPublishableMetadata).ToList(),
                    Keywords = keywords,
                    DateCreated = dateCreated,
                    DateUpdated = dateUpdated
                });
        }

        private static List<string> ExtractKeywords(Photo sourcePhoto)
        {
            PhotoMetadata kwd = sourcePhoto.Metadata.FirstOrDefault(
                item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Keywords));
            if (kwd != null)
            {
                return kwd.Value.Split(',').Where(IsValidKeywordName).ToList();
            }

            return new List<string>();
        }

        private static bool IsValidKeywordName(string s)
        {
            return !string.IsNullOrWhiteSpace(s);
        }

        private static bool IsPublishableMetadata(PhotoMetadata metadata)
        {
            var notPublishable = new[]
                {
                    MetadataNames.Title,
                    MetadataNames.DateTaken,
                    MetadataNames.Keywords,
                    MetadataNames.Rating,
                    MetadataNames.Latitude,
                    MetadataNames.Longitude,
                    MetadataNames.Comment
                };

            return notPublishable.All(item => !StringComparer.InvariantCultureIgnoreCase.Equals(item, metadata.Name));
        }

        private static int ExtractRating(Photo sourcePhoto)
        {
            int rating = 1;
            PhotoMetadata rat = sourcePhoto.Metadata.FirstOrDefault(
                item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Rating));
            if (rat != null)
            {
                if (!int.TryParse(rat.Value, out rating) || rating < 1 || rating > 5)
                {
                    rating = 1;
                }
            }
            return rating;
        }

        private static void ExtractDates(Photo sourcePhoto, out DateTime dateCreated, out DateTime dateUpdated)
        {
            dateCreated = sourcePhoto.Files.Min(file => file.LastModified);
            dateUpdated = sourcePhoto.Files.Max(file => file.LastModified);

            PhotoMetadata taken =
                sourcePhoto.Metadata.FirstOrDefault(
                    item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.DateTaken));
            if (taken != null)
            {
                // Extract the date from the value;
                DateTime when;
                if (DateTime.TryParse(taken.Value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces,
                                      out when))
                {
                    if (when < dateCreated)
                    {
                        dateCreated = when;
                    }

                    if (when > dateUpdated)
                    {
                        dateUpdated = when;
                    }
                }
            }
        }

        private static string ExtractTitle(Photo sourcePhoto)
        {
            string description = string.Empty;
            PhotoMetadata desc =
                sourcePhoto.Metadata.FirstOrDefault(
                    item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Title));
            if (desc != null)
            {
                description = desc.Value;
            }

            return description;
        }

        private static string ExtractDescription(Photo sourcePhoto)
        {
            string description = string.Empty;
            PhotoMetadata desc =
                sourcePhoto.Metadata.FirstOrDefault(
                    item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Comment));
            if (desc != null)
            {
                description = desc.Value;
            }
            return description;
        }

        private static Location ExtractLocation(Photo sourcePhoto)
        {
            Location location = null;
            PhotoMetadata lat = sourcePhoto.Metadata.FirstOrDefault(
                item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Latitude));
            PhotoMetadata lng = sourcePhoto.Metadata.FirstOrDefault(
                item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Longitude));
            if (lat != null && lng != null)
            {
                double latitude;
                double longitude;
                if (double.TryParse(lat.Value, out latitude) && double.TryParse(lng.Value, out longitude))
                {
                    location = new Location
                        {
                            Latitude = latitude,
                            Longitude = longitude
                        };
                }
            }
            return location;
        }

        private static void EnsureParentFoldersExist(string[] pathFragments, string[] breadcrumbFragments,
                                                     Dictionary<string, GalleryEntry> contents)
        {
            for (int folderLevel = 1; folderLevel < pathFragments.Length; ++folderLevel)
            {
                string level = EnsureTerminatedPath("/" + string.Join("/", pathFragments.Take(folderLevel)));

                GalleryEntry item;
                if (!contents.TryGetValue(level, out item))
                {
                    string parentLevel =
                        EnsureTerminatedPath("/" + string.Join("/", pathFragments.Take(folderLevel - 1)));

                    AppendEntry(contents, parentLevel, level, new GalleryEntry
                        {
                            Path = level,
                            Title = breadcrumbFragments[folderLevel - 1],
                            Description = string.Empty,
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
                    Path = "/",
                    Title = "Mark's Photos",
                    Description = "Photos taken by Mark Ridgwell.",
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