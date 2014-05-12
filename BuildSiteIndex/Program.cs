using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using BuildSiteIndex.Properties;
using FileNaming;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Raven.Client;
using Raven.Client.Embedded;
using StorageHelpers;
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


            AppendRootEntry(contents);

            const string albumsRoot = "albums";
            const string albumsTitle = "Albums";

            const string keywordsRoot = "keywords";
            const string keywordsTitle = "Keywords";

            var keywords = new Dictionary<string, KeywordEntry>();

            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                foreach (Photo sourcePhoto in inputSession.GetAll<Photo>())
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

                    PhotoMetadata keywordMetadata =
                        sourcePhoto.Metadata.FirstOrDefault(candidate => candidate.Name == MetadataNames.Keywords);
                    if (keywordMetadata != null)
                    {
                        foreach (
                            string keyword in
                                keywordMetadata.Value.Replace(';', ',').Split(',')
                                               .Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
                        {
                            KeywordEntry entry;
                            if (!keywords.TryGetValue(keyword.ToLowerInvariant(), out entry))
                            {
                                entry = new KeywordEntry(keyword);
                                keywords.Add(keyword.ToLowerInvariant(), entry);
                            }

                            entry.Photos.Add(sourcePhoto);
                        }
                    }
                }
            }

            Console.WriteLine("Found {0} items total", contents.Count);
            Console.WriteLine("Found {0} keyword items total", keywords.Count);

            foreach (KeywordEntry keyword in keywords.Values)
            {
                foreach (Photo sourcePhoto in keyword.Photos)
                {
                    string path =
                        EnsureTerminatedPath("/" + keywordsRoot + "/" + keyword.Keyword.ToLowerInvariant() + "/" +
                                             sourcePhoto.UrlSafePath);
                    string breadcrumbs =
                        EnsureTerminatedBreadcrumbs("\\" + keywordsTitle + "\\" + keyword.Keyword + "\\" +
                                                    sourcePhoto.BasePath);
                    Console.WriteLine("Item: {0}", path);

                    string[] pathFragments = path.Split('/').Where(IsNotEmpty).ToArray();
                    string[] breadcrumbFragments = breadcrumbs.Split('\\').Where(IsNotEmpty).ToArray();
                }
            }

            AddCoordinatesFromChildren(contents);
            ProduceJsonFile(contents);

            documentStoreInput.Backup(Settings.Default.DatabaseBackupFolder);
        }

        private static void AddCoordinatesFromChildren(Dictionary<string, GalleryEntry> contents)
        {
            foreach (GalleryEntry entry in contents.Values.Where(candidate => candidate.Location == null))
            {
                if (entry.Children != null && entry.Children.Any())
                {
                    var locations = new List<Location>();

                    AppendChildLocations(entry, locations);

                    Location location = LocationHelpers.GetCenterFromDegrees(locations);
                    if (location != null)
                    {
                        entry.Location = location;
                    }
                }
            }
        }

        private static void AppendChildLocations(GalleryEntry entry, List<Location> locations)
        {
            foreach (GalleryEntry child in entry.Children)
            {
                if (child.Location != null)
                {
                    locations.Add(child.Location);
                }

                if (child.Children != null && child.Children.Any())
                {
                    AppendChildLocations(child, locations);
                }
            }
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
                                                 where !IsHiddenItem(childRecord)
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
                byte[] originalBytes = File.ReadAllBytes(outputFilename);
                string decoded = Encoding.UTF8.GetString(originalBytes);
                if (decoded == json)
                {
                    Console.WriteLine("No changes since last run");
                    return;
                }
                else
                {
                    GallerySiteIndex oldData = null;
                    try
                    {
                        oldData = JsonConvert.DeserializeObject<GallerySiteIndex>(decoded);
                    }
                    catch (Exception)
                    {
                    }

                    if (oldData != null)
                    {
                        List<string> deletedItems = FindDeletedItems(oldData, data);
                        data.deletedItems.AddRange(deletedItems.OrderBy(x => x));

                        UploadChanges(data, oldData);

                        UploadItemsToDelete(data, deletedItems);
                    }
                    else
                    {
                        UploadAllItems(data);
                    }
                }
            }
            else
            {
                UploadAllItems(data);
            }

            byte[] encoded = Encoding.UTF8.GetBytes(json);
            File.WriteAllBytes(outputFilename, encoded);
        }

        private static bool IsHiddenItem(GalleryChildItem item)
        {
            return IsHiddenItem(item.Path);
        }

        private static bool IsHiddenItem(GalleryEntry item)
        {
            return IsHiddenItem(item.Path);
        }

        private static bool IsHiddenItem(string path)
        {
            return path == "/albums/private/";
        }

        private static void UploadItemsToDelete(GallerySiteIndex data, List<string> deletedItems)
        {
            const int batchSize = 100;
            List<string> batch = null;
            foreach (string deletedItem in deletedItems)
            {
                if (batch == null)
                {
                    batch = new List<string>();
                }

                batch.Add(deletedItem);

                if (batch.Count >= batchSize)
                {
                    UploadDeletionBatch(data, batch);
                    batch = null;
                }
            }

            if (batch != null && batch.Count != 0)
            {
                UploadDeletionBatch(data, batch);
            }
        }

        private static List<string> FindDeletedItems(GallerySiteIndex oldData, GallerySiteIndex data)
        {
            List<string> oldItems = oldData.items.Select(r => r.Path).ToList();
            List<string> newItems = data.items.Select(r => r.Path).ToList();

            List<string> deletedItems = oldItems.Where(oldItem => !newItems.Contains(oldItem)).ToList();

            if (oldData.deletedItems != null)
            {
                foreach (string oldDeletedItem in oldData.deletedItems)
                {
                    if (!newItems.Contains(oldDeletedItem) && !deletedItems.Contains(oldDeletedItem))
                    {
                        deletedItems.Add(oldDeletedItem);
                    }
                }
            }
            return deletedItems;
        }

        private static void UploadAllItems(GallerySiteIndex data)
        {
            foreach (
                GalleryItem item in UploadOrdering(data))
            {
                UploadOneItem(data, item);
            }
        }

        private static IOrderedEnumerable<GalleryItem> UploadOrdering(GallerySiteIndex data)
        {
            return data.items.OrderBy(StrictTypeOrdering)
                       .OrderBy(candidate => candidate.Path);
        }

        private static int StrictTypeOrdering(GalleryItem candidate)
        {
            // Photos first... albums after
            return candidate.Type == "photo" ? 1 : 2;
        }

        private static void UploadChanges(GallerySiteIndex data, GallerySiteIndex oldData)
        {
            foreach (
                GalleryItem item in UploadOrdering(data))
            {
                GalleryItem oldItem = oldData.items.FirstOrDefault(candidate => candidate.Path == item.Path);
                if (oldItem == null || !ItemUpdateHelpers.AreSame(oldItem, item))
                {
                    UploadOneItem(data, item);
                }
            }
        }

        private static void UploadOneItem(GallerySiteIndex data, GalleryItem item)
        {
            GallerySiteIndex itemToPost = CreateItemToPost(data, item);

            string progressText = item.Path;

            UploadItem(itemToPost, progressText);
        }

        private static void UploadItem(GallerySiteIndex itemToPost, string progressText)
        {
            //var handler = new HttpClientHandler
            //{
            //    UseDefaultCredentials = false,
            //    Proxy = new WebProxy("http://localhost:8888", false, new string[] { }),
            //    UseProxy = true
            //};

            using (var client = new HttpClient
                {
                    BaseAddress = new Uri(Settings.Default.WebServerBaseAddress)
                })
            {
                Console.WriteLine("Uploading: {0}", progressText);

                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));


                var formatter = new JsonMediaTypeFormatter
                    {
                        SerializerSettings = {ContractResolver = new DefaultContractResolver()}
                    };

                var content = new ObjectContent<GallerySiteIndex>(itemToPost, formatter);

                HttpResponseMessage response = client.PostAsync("tasks/sync", content).Result;
                Console.WriteLine("Status: {0}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                }
            }
        }


        private static GallerySiteIndex CreateItemToPost(GallerySiteIndex data, GalleryItem item)
        {
            var itemToPost = new GallerySiteIndex
                {
                    version = data.version,
                    items = new List<GalleryItem>
                        {
                            item
                        },
                    deletedItems = new List<string>()
                };
            return itemToPost;
        }

        private static GallerySiteIndex CreateItemToPost(GallerySiteIndex data, List<string> itemsToDelete)
        {
            var itemToPost = new GallerySiteIndex
                {
                    version = data.version,
                    items = new List<GalleryItem>(),
                    deletedItems = new List<string>(itemsToDelete)
                };
            return itemToPost;
        }

        private static void UploadDeletionBatch(GallerySiteIndex data, List<string> batch)
        {
            GallerySiteIndex itemToPost = CreateItemToPost(data, batch);

            string progressText = string.Format("Deletion batch of size {0} starting with {1}", batch.Count,
                                                batch.FirstOrDefault());

            UploadItem(itemToPost, progressText);
        }

        private static GalleryChildItem GetNextItem(List<GalleryEntry> siblings, GalleryEntry parentRecord,
                                                    GalleryChildItem lastItem)
        {
            GalleryChildItem candidate = siblings.SkipWhile(x => x != parentRecord)
                                                 .Skip(1)
                                                 .Select(CreateGalleryChildItem)
                                                 .FirstOrDefault(item => !IsHiddenItem(item));

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
                .Select(CreateGalleryChildItem).FirstOrDefault(item => !IsHiddenItem(item));

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
                .Select(CreateGalleryChildItem).LastOrDefault(item => !IsHiddenItem(item));

            return SkipKnownItem(candidate, parentRecord);
        }

        private static GalleryChildItem GetPreviousItem(List<GalleryEntry> siblings, GalleryEntry parentRecord,
                                                        GalleryChildItem firstItem)
        {
            GalleryChildItem candidate = siblings.TakeWhile(x => x != parentRecord)
                                                 .Select(CreateGalleryChildItem)
                                                 .LastOrDefault(item => !IsHiddenItem(item));

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
                    Metadata =
                        sourcePhoto.Metadata.Where(IsPublishableMetadata)
                                   .OrderBy(item => item.Name.ToLowerInvariant())
                                   .ToList(),
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
                return kwd.Value.Replace(';', ',').Split(',').Where(IsValidKeywordName).ToList();
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
                            Title = breadcrumbFragments[folderLevel - 1].ReformatTitle(DateFormat.LongDate),
                            Description = string.Empty,
                            Location = null,
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
                    Location = null,
                    Children = new List<GalleryEntry>(),
                    DateCreated = DateTime.MaxValue,
                    DateUpdated = DateTime.MinValue
                };

            contents.Add("/", entry);
        }

        public class ConverterContractResolver : DefaultContractResolver
        {
            public static readonly ConverterContractResolver Instance = new ConverterContractResolver();

            protected override JsonContract CreateContract(Type objectType)
            {
                JsonContract contract = base.CreateContract(objectType);

                // this will only be called once and then cached
                if (objectType == typeof (DateTime) || objectType == typeof (DateTimeOffset))
                    contract.Converter = new JavaScriptDateTimeConverter();

                return contract;
            }
        }
    }
}