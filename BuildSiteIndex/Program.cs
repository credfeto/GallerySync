﻿using System;
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
        private const string AlbumsRoot = "albums";
        private const string AlbumsTitle = "Albums";

        private const string KeywordsRoot = "keywords";
        private const string KeywordsTitle = "Keywords";
        private const int GalleryJsonVersion = 1;
        private const int MaxDailyUploads = 5000;

        private static readonly object EntryLock = new object();

        private static bool _ignoreExisting;

        private static int Main(string[] args)
        {
            Console.WriteLine("BuildSiteIndex");

            if (args != null &&
                args.Any(candidate => StringComparer.InvariantCultureIgnoreCase.Equals(candidate, "IgnoreExisting")))
            {
                Console.WriteLine("******* Ignoring existing items *******");
                _ignoreExisting = true;
            }

            BoostPriority();

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


            var keywords = new Dictionary<string, KeywordEntry>();

            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                foreach (Photo sourcePhoto in inputSession.GetAll<Photo>())
                {
                    string path = EnsureTerminatedPath("/" + AlbumsRoot + "/" + sourcePhoto.UrlSafePath);
                    string breadcrumbs = EnsureTerminatedBreadcrumbs("\\" + AlbumsTitle + "\\" + sourcePhoto.BasePath);
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

                    if (!IsUnderHiddenItem(path))
                    {
                        AppendKeywordsForLaterProcessing(sourcePhoto, keywords);
                    }
                }
            }

            Console.WriteLine("Found {0} items total", contents.Count);
            Console.WriteLine("Found {0} keyword items total", keywords.Count);

            BuildGalleryItemsForKeywords(keywords, contents);

            AddCoordinatesFromChildren(contents);
            ProcessSiteIndex(contents, documentStoreInput);

            UploadQueuedItems(documentStoreInput);

            documentStoreInput.Backup(Settings.Default.DatabaseBackupFolder);
        }

        private static void UploadQueuedItems(EmbeddableDocumentStore documentStoreInput)
        {
            int itemsUploaded = 0;

            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                foreach (UploadQueueItem item in inputSession.GetAll<UploadQueueItem>())
                {
                    ++itemsUploaded;
                    if (itemsUploaded > MaxDailyUploads)
                    {
                        Console.WriteLine("********** REACHED MAX DailyUploads **********");
                        return;
                    }

                    string key = BuildUploadQueueHash(item.Item);

                    using (IDocumentSession updateSession = documentStoreInput.OpenSession())
                    {
                        var updateItem = updateSession.Load<UploadQueueItem>(key);
                        if (updateItem != null)
                        {
                            if (UploadOneItem(updateItem))
                            {
                                updateSession.Delete(updateItem);
                                updateSession.SaveChanges();
                            }
                        }
                    }
                }
            }
        }

        private static void BuildGalleryItemsForKeywords(Dictionary<string, KeywordEntry> keywords,
                                                         Dictionary<string, GalleryEntry> contents)
        {
            foreach (KeywordEntry keyword in keywords.Values)
            {
                foreach (Photo sourcePhoto in keyword.Photos)
                {
                    string sourcePhotoFullPath = EnsureTerminatedPath("/" + AlbumsRoot + "/" + sourcePhoto.UrlSafePath);
                    string sourcePhotoBreadcrumbs =
                        EnsureTerminatedBreadcrumbs("\\" + AlbumsTitle + "\\" + sourcePhoto.BasePath);
                    string[] sourcePhotoPathFragments = sourcePhotoFullPath.Split('/').Where(IsNotEmpty).ToArray();
                    string[] sourcePhotoBreadcrumbFragments =
                        sourcePhotoBreadcrumbs.Split('\\').Where(IsNotEmpty).ToArray();


                    string keywordLower =
                        UrlNaming.BuildUrlSafePath(keyword.Keyword.ToLowerInvariant())
                                 .TrimEnd("/".ToArray())
                                 .TrimStart("-".ToArray())
                                 .TrimEnd("-".ToArray());

                    string firstKeywordCharLower = keywordLower.Substring(0, 1).ToLowerInvariant();
                    string firstKeywordCharUpper = keywordLower.Substring(0, 1).ToUpperInvariant();

                    string path =
                        EnsureTerminatedPath("/" + KeywordsRoot + "/" + firstKeywordCharLower + "/" + keywordLower + "/" +
                                             sourcePhotoPathFragments[sourcePhotoPathFragments.Length - 2] + "-" +
                                             sourcePhotoPathFragments.Last());

                    string title = sourcePhotoBreadcrumbFragments.Last();
                    string parentTitle =
                        sourcePhotoBreadcrumbFragments[sourcePhotoBreadcrumbFragments.Length - 2].ExtractDate(
                            DateFormat.LongDate);
                    if (!string.IsNullOrWhiteSpace(parentTitle))
                    {
                        title += " (" + parentTitle + ")";
                    }

                    string breadcrumbs =
                        EnsureTerminatedBreadcrumbs("\\" + KeywordsTitle + "\\" + firstKeywordCharUpper + "\\" +
                                                    keyword.Keyword + "\\" +
                                                    title);


                    string[] pathFragments = path.Split('/').Where(IsNotEmpty).ToArray();
                    string[] breadcrumbFragments = breadcrumbs.Split('\\').Where(IsNotEmpty).ToArray();

                    EnsureParentFoldersExist(pathFragments, breadcrumbFragments, contents);

                    string parentLevel =
                        EnsureTerminatedPath("/" + string.Join("/", pathFragments.Take(pathFragments.Length - 1)));

                    Console.WriteLine("Item: {0}", path);

                    AppendVirtualEntry(contents, parentLevel, path, sourcePhotoFullPath,
                                       title,
                                       sourcePhoto);
                }
            }
        }

        private static void AppendKeywordsForLaterProcessing(Photo sourcePhoto,
                                                             Dictionary<string, KeywordEntry> keywords)
        {
            PhotoMetadata keywordMetadata =
                sourcePhoto.Metadata.FirstOrDefault(candidate => candidate.Name == MetadataNames.Keywords);
            if (keywordMetadata != null)
            {
                foreach (
                    string keyword in
                        keywordMetadata.Value.Replace(';', ',').Split(',')
                                       .Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
                {
                    string safe =
                        UrlNaming.BuildUrlSafePath(keyword.ToLowerInvariant())
                                 .TrimStart("-".ToArray())
                                 .TrimEnd("-".ToArray());

                    KeywordEntry entry;
                    if (!keywords.TryGetValue(safe, out entry))
                    {
                        entry = new KeywordEntry(keyword);
                        keywords.Add(safe, entry);
                    }

                    entry.Photos.Add(sourcePhoto);
                }
            }
        }

        private static GalleryEntry FindParentAlbumPath(Dictionary<string, GalleryEntry> contents, Photo parentRecord)
        {
            string path = parentRecord.BasePath;

            GalleryEntry item;
            if (!contents.TryGetValue(path, out item) || item == null)
            {
                // can't find the full path: give up
                return null;
            }

            return item;
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
                if (child.Children != null && child.Children.Any())
                {
                    AppendChildLocations(child, locations);
                }
                else if (child.Location != null)
                {
                    // only add locations for photos
                    locations.Add(child.Location);
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

        private static void ProcessSiteIndex(Dictionary<string, GalleryEntry> contents,
                                             EmbeddableDocumentStore documentStoreInput)
        {
            GallerySiteIndex data = ProduceSiteIndex(contents);

            string outputFilename = Path.Combine(Settings.Default.OutputFolder, "site.js");

            string json = JsonConvert.SerializeObject(data);
            if (!_ignoreExisting || File.Exists(outputFilename))
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

                        QueueUploadChanges(data, oldData, documentStoreInput);

                        QueueUploadItemsToDelete(data, deletedItems, documentStoreInput);
                    }
                    else
                    {
                        QueueUploadAllItems(data, documentStoreInput);
                    }
                }
            }
            else
            {
                QueueUploadAllItems(data, documentStoreInput);
            }

            ExtensionMethods.RotateLastGenerations(outputFilename);

            byte[] encoded = Encoding.UTF8.GetBytes(json);
            File.WriteAllBytes(outputFilename, encoded);
        }

        private static GallerySiteIndex ProduceSiteIndex(Dictionary<string, GalleryEntry> contents)
        {
            return new GallerySiteIndex
                {
                    version = GalleryJsonVersion,
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
                                     OriginalAlbumPath = parentRecord.OriginalAlbumPath,
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
                                     Breadcrumbs = ExtractItemPreadcrumbs(contents, parentRecord)
                                 }).ToList(),
                    deletedItems = new List<string>()
                };
        }

        private static List<GalleryChildItem> ExtractItemPreadcrumbs(Dictionary<string, GalleryEntry> contents,
                                                                     GalleryEntry parentRecord)
        {
            var items = new List<GalleryChildItem>();

            string[] breadcrumbFragments = parentRecord.Path.Split('/').Where(IsNotEmpty).ToArray();

            for (int folderLevel = 1; folderLevel < breadcrumbFragments.Length; ++folderLevel)
            {
                string level = EnsureTerminatedPath("/" + string.Join("/", breadcrumbFragments.Take(folderLevel)));

                GalleryEntry item;
                if (!contents.TryGetValue(level, out item) || item == null)
                {
                    // can't find the full path: give up
                    return new List<GalleryChildItem>();
                }

                items.Add(CreateGalleryChildItem(item));
            }

            return items;
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

        private static bool IsUnderHiddenItem(string path)
        {
            return path.StartsWith("/albums/private/", StringComparison.OrdinalIgnoreCase);
        }

        private static void QueueUploadItemsToDelete(GallerySiteIndex data, List<string> deletedItems,
                                                     EmbeddableDocumentStore documentStoreInput)
        {
            foreach (string path in deletedItems)
            {
                QueueUploadOneItem(data, new GalleryItem
                    {
                        Path = path
                    },
                                   UploadType.DeleteItem,
                                   documentStoreInput
                    );
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

        private static void QueueUploadAllItems(GallerySiteIndex data, EmbeddableDocumentStore documentStoreInput)
        {
            foreach (
                GalleryItem item in UploadOrdering(data))
            {
                QueueUploadOneItem(data, item, UploadType.NewItem, documentStoreInput);
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

        private static void QueueUploadChanges(GallerySiteIndex data, GallerySiteIndex oldData,
                                               EmbeddableDocumentStore documentStoreInput)
        {
            foreach (
                GalleryItem item in UploadOrdering(data))
            {
                GalleryItem oldItem = oldData.items.FirstOrDefault(candidate => candidate.Path == item.Path);
                if (oldItem == null || !ItemUpdateHelpers.AreSame(oldItem, item))
                {
                    QueueUploadOneItem(data, item, oldItem == null ? UploadType.NewItem : UploadType.UpdateItem,
                                       documentStoreInput);
                }
            }
        }

        public static void QueueUploadOneItem(GallerySiteIndex data, GalleryItem item, UploadType uploadType,
                                              EmbeddableDocumentStore documentStoreInput)
        {
            using (IDocumentSession session = documentStoreInput.OpenSession())
            {
                string key = BuildUploadQueueHash(item);

                var existingItem = session.Load<UploadQueueItem>(key);
                if (existingItem == null)
                {
                    existingItem = new UploadQueueItem
                        {
                            Version = data.version,
                            Item = item,
                            UploadType = uploadType
                        };
                    session.Store(existingItem, key);
                }
                else
                {
                    existingItem.Version = data.version;
                    existingItem.Item = item;
                    existingItem.UploadType = uploadType;
                    session.Store(existingItem, key);
                }

                session.SaveChanges();
            }
        }

        private static string BuildUploadQueueHash(GalleryItem item)
        {
            return "UploadQueue" + Hasher.HashBytes(Encoding.UTF8.GetBytes(item.Path));
        }

        private static bool UploadOneItem(UploadQueueItem item)
        {
            GallerySiteIndex itemToPost = CreateItemToPost(item);

            string progressText = item.Path;

            const int maxRetries = 5;
            bool uploaded = false;
            int retry = 0;
            do
            {
                uploaded = UploadItem(itemToPost, progressText, item.UploadType);
                ++retry;
            } while (!uploaded && retry < maxRetries);
            return uploaded;
        }

        private static bool UploadItem(GallerySiteIndex itemToPost, string progressText, UploadType uploadType)
        {
            //var handler = new HttpClientHandler
            //{
            //    UseDefaultCredentials = false,
            //    Proxy = new WebProxy("http://localhost:8888", false, new string[] { }),
            //    UseProxy = true
            //};

            try
            {
                using (var client = new HttpClient
                    {
                        BaseAddress = new Uri(Settings.Default.WebServerBaseAddress),
                        Timeout = TimeSpan.FromSeconds(200)
                    })
                {
                    Console.WriteLine("Uploading ({0}): {1}", MakeUploadTypeText(uploadType), progressText);

                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));


                    var formatter = new JsonMediaTypeFormatter
                        {
                            SerializerSettings = {ContractResolver = new DefaultContractResolver()},
                            Indent = false
                        };

                    var content = new ObjectContent<GallerySiteIndex>(itemToPost, formatter);

                    HttpResponseMessage response = client.PostAsync("tasks/sync", content).Result;
                    Console.WriteLine("Status: {0}", response.StatusCode);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: {0}", exception.Message);
                return false;
            }
        }

        private static string MakeUploadTypeText(UploadType uploadType)
        {
            switch (uploadType)
            {
                case UploadType.NewItem:
                    return "New";

                case UploadType.DeleteItem:
                    return "Delete";

                default:
                    return "Existing";
            }
        }


        private static GallerySiteIndex CreateItemToPost(UploadQueueItem item)
        {
            var itemToPost = new GallerySiteIndex
                {
                    version = item.Version,
                    items = new List<GalleryItem>(),
                    deletedItems = new List<string>()
                };

            if (item.UploadType == UploadType.DeleteItem)
            {
                itemToPost.deletedItems.Add(item.Item.Path);
            }
            else
            {
                itemToPost.items.Add(item.Item);
            }

            return itemToPost;
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
                    OriginalAlbumPath = firstRecord.OriginalAlbumPath,
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

            if (IsUnderHiddenItem(path))
            {
                keywords = new List<string>();
            }

            AppendEntry(contents, parentLevel, path, new GalleryEntry
                {
                    Path = path,
                    OriginalAlbumPath = null,
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

        private static void AppendVirtualEntry(Dictionary<string, GalleryEntry> contents, string parentLevel,
                                               string path, string originalPath,
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
                    OriginalAlbumPath = originalPath,
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
                            OriginalAlbumPath = null,
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
            lock (EntryLock)
            {
                GalleryEntry parent;
                if (!contents.TryGetValue(parentPath, out parent))
                {
                    throw new ApplicationException("Could not find: " + parentPath);
                }

                GalleryEntry current;
                if (contents.TryGetValue(itemPath, out current))
                {
                    // This shouldn't ever happen, but wth, it does!
                    Console.WriteLine("ERROR: DUPLICATE PATH: {0}", itemPath);
                    return;
                }

                Console.WriteLine(" * Path: {0}", itemPath);
                Console.WriteLine("   + Title: {0}", entry.Title);
                parent.Children.Add(entry);

                contents.Add(itemPath, entry);
            }
        }

        private static void AppendRootEntry(Dictionary<string, GalleryEntry> contents)
        {
            lock (EntryLock)
            {
                var entry = new GalleryEntry
                    {
                        Path = "/",
                        OriginalAlbumPath = null,
                        Title = "Mark's Photos",
                        Description = "Photos taken by Mark Ridgwell.",
                        Location = null,
                        Children = new List<GalleryEntry>(),
                        DateCreated = DateTime.MaxValue,
                        DateUpdated = DateTime.MinValue
                    };

                contents.Add("/", entry);
            }
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