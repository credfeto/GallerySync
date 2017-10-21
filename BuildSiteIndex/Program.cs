using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using BuildSiteIndex.Properties;
using FileNaming;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using StorageHelpers;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace BuildSiteIndex
{
    internal class Program
    {
        private const string AlbumsRoot = "albums";
        private const string AlbumsTitle = "Albums";

        private const string KeywordsRoot = "keywords";
        private const string KeywordsTitle = "Keywords";

        private const string EventsRoot = "events";
        private const string EventsTitle = "Events";

        private const int GalleryJsonVersion = 1;

        private const int MaxPhotosPerKeyword = 1000;
        private static int _maxDailyUploads = 8000;

        private static readonly EventDesc[] _events =
        {
            new EventDesc
            {
                Name = "Linkfest",

                PathMatch = new Regex(@"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(linkfest-harlow)-",
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description =
                    "[Linkfest](http://www.linkfestharlow.co.uk/), a free music festival in Harlow Town Park at the bandstand."
            },

            new EventDesc
            {
                Name = "Barleylands - Essex Country Show",

                PathMatch = new Regex(@"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(barleylands-essex-country-show)-",
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description =
                    "[Essex Country show](http://www.barleylands.co.uk/essex-country-show) at Barleylands, Billericay."
            },

            new EventDesc
            {
                Name = "Moreton Boxing Day Tug Of War",

                PathMatch = new Regex(@"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(moreton-boxing-day-tug-of-war)-",
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description = "The annual tug-of war over the Cripsey Brook at Moreton, Essex."
            },

            new EventDesc
            {
                Name = "Greenwich Tall Ships Festival",

                PathMatch = new Regex(@"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(greenwich-tall-ships-festival)-",
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description = ""
            },

            new EventDesc
            {
                Name = "Rock School - Lets Rock The Park",

                PathMatch = new Regex(@"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(rock-school-lets-rock-the-park)-",
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description = ""
            }
        };

        private static bool _ignoreExisting;

        private static readonly SemaphoreSlim _entrySemaphore = new SemaphoreSlim(1);

        private static int Main(string[] args)
        {
            Console.WriteLine("BuildSiteIndex");

            if (args != null)
            {
                if (args.Any(candidate =>
                    StringComparer.InvariantCultureIgnoreCase.Equals(candidate, "IgnoreExisting")))
                {
                    Console.WriteLine("******* Ignoring existing items *******");
                    _ignoreExisting = true;
                }

                if (args.Any(candidate => StringComparer.InvariantCultureIgnoreCase.Equals(candidate, "NoLimit")))
                {
                    Console.WriteLine("******* Ignoring Upload limit *******");
                    _maxDailyUploads = int.MaxValue;
                }
            }


            BoostPriority();

            try
            {
                ProcessGallery().GetAwaiter().GetResult();

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

        private static async Task ProcessGallery()
        {
            var contents = new Dictionary<string, GalleryEntry>();

            var target = await LoadRepository(Settings.Default.DatabaseInputFolder);

            await AppendRootEntry(contents);

            var keywords = new Dictionary<string, KeywordEntry>();

            foreach (var sourcePhoto in target)
            {
                var path = EnsureTerminatedPath("/" + AlbumsRoot + "/" + sourcePhoto.UrlSafePath);
                var breadcrumbs = EnsureTerminatedBreadcrumbs("\\" + AlbumsTitle + "\\" + sourcePhoto.BasePath);
                Console.WriteLine("Item: {0}", path);

                var pathFragments = path.Split('/').Where(IsNotEmpty).ToArray();
                var breadcrumbFragments = breadcrumbs.Split('\\').Where(IsNotEmpty).ToArray();

                await EnsureParentFoldersExist(pathFragments, breadcrumbFragments, contents);

                var parentLevel =
                    EnsureTerminatedPath("/" + string.Join("/", pathFragments.Take(pathFragments.Length - 1)));

                var title = ExtractTitle(sourcePhoto);
                if (string.IsNullOrWhiteSpace(title))
                    title = breadcrumbFragments[breadcrumbFragments.Length - 1];

                await AppendPhotoEntry(contents, parentLevel, path,
                    title,
                    sourcePhoto);

                if (!IsUnderHiddenItem(path))
                    AppendKeywordsForLaterProcessing(sourcePhoto, keywords);
            }

            Console.WriteLine("Found {0} items total", contents.Count);
            Console.WriteLine("Found {0} keyword items total", keywords.Count);

            await Task.WhenAll(
                BuildEvents(contents),
                BuildGalleryItemsForKeywords(keywords, contents));

            AddCoordinatesFromChildren(contents);
            ProcessSiteIndex(contents);

            var documentStoreInput = LoadQueuedItems();
            await UploadQueuedItems(documentStoreInput);

            //documentStoreInput.Backup(Settings.Default.DatabaseBackupFolder);
        }

        private static List<UploadQueueItem> LoadQueuedItems()
        {
            var files = Directory.EnumerateFiles(Settings.Default.QueueFolder, "*.queue");

            var loaded = new List<UploadQueueItem>();

            foreach (var file in files)
            {
                var bytes = FileHelpers.ReadAllBytes(file);

                var item = JsonConvert.DeserializeObject<UploadQueueItem>(Encoding.UTF8.GetString(bytes));

                loaded.Add(item);
            }

            return loaded;
        }

        private static async Task<Photo[]> LoadRepository(string baseFolder)
        {
            Console.WriteLine("Loading Repository from {0}...", baseFolder);
            var scores = new[]
            {
                ".info"
            };

            var sidecarFiles = new List<string>();

            var emitter = new PhotoInfoEmitter(baseFolder);

            if (Directory.Exists(baseFolder))
            {
                var filesFound = await DirectoryScanner.ScanFolder(baseFolder, emitter, scores.ToList(), sidecarFiles);

                Console.WriteLine("{0} : Files Found: {1}", baseFolder, filesFound);
            }

            return emitter.Photos;
        }

        private static async Task BuildEvents(Dictionary<string, GalleryEntry> contents)
        {
            foreach (var folder in contents.Values.Where(UnderAlbumsFolder).Where(HasPhotoChildren).ToList())
            {
                if (IsUnderHiddenItem(folder.Path))
                    continue;

                EventDesc found = null;
                foreach (var eventEntry in _events)
                    if (eventEntry.PathMatch.IsMatch(folder.Path))
                    {
                        found = eventEntry;
                        break;
                    }

                if (found != null)
                {
                    Console.WriteLine("Found {0} in {1}", found.Name, folder.Path);

                    var pathMatch = found.PathMatch.Match(folder.Path);

                    var year = pathMatch.Groups[2];
                    var month = pathMatch.Groups[3];
                    var day = pathMatch.Groups[4];
                    var title = pathMatch.Groups[5];

                    var pathStart = pathMatch.Groups[0];

                    var pathRest = folder.Path.Substring(pathStart.Length).Trim().TrimEnd('/');
                    if (string.IsNullOrWhiteSpace(pathRest))
                        pathRest = title.ToString().Trim();

                    var date = year + "-" + month + "-" + day;
                    var titleDate = (date + " MTR").ReformatTitle(DateFormat.LongDate).Replace(" - MTR", string.Empty);


                    foreach (var sourcePhoto in folder.Children.Where(IsImage))
                    {
                        var path =
                            EnsureTerminatedPath(
                                UrlNaming.BuildUrlSafePath(
                                    "/" + EventsRoot + "/" + found.Name + "/" +
                                    year + "/" + date + "/" +
                                    pathRest + "/" +
                                    sourcePhoto.Title));
                        var breadcrumbs =
                            EnsureTerminatedBreadcrumbs("\\" + EventsTitle + "\\" + found.Name + "\\" + year + "\\" +
                                                        titleDate + "\\" +
                                                        folder.Title.Replace(titleDate + " - ", string.Empty) + "\\" +
                                                        sourcePhoto.Title);


                        var pathFragments = path.Split('/').Where(IsNotEmpty).ToArray();
                        var breadcrumbFragments = breadcrumbs.Split('\\').Where(IsNotEmpty).ToArray();

                        await EnsureParentFoldersExist(pathFragments, breadcrumbFragments, contents);

                        var parentLevel =
                            EnsureTerminatedPath("/" + string.Join("/", pathFragments.Take(pathFragments.Length - 1)));

                        Console.WriteLine("Item: {0}", path);

                        await AppendVirtualEntryPhotoForGalleryEntry(contents, parentLevel, path, sourcePhoto.Path,
                            sourcePhoto.Title,
                            sourcePhoto);
                    }
                }
            }
        }

        private static bool HasChildren(GalleryEntry item)
        {
            return item.Children != null && item.Children.Any();
        }

        private static bool HasPhotoChildren(GalleryEntry item)
        {
            return HasChildren(item) &&
                   item.Children.Any(IsImage);
        }

        private static bool UnderAlbumsFolder(GalleryEntry item)
        {
            return item.Path.StartsWith("/albums/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImage(GalleryEntry candiate)
        {
            return candiate.ImageSizes != null && candiate.ImageSizes.Any();
        }

        private static async Task UploadQueuedItems(List<UploadQueueItem> inputSession)
        {
            var context = new LoadContext();

            // Only upload creates and updates.
            foreach (var item in inputSession.Where(item => item.UploadType != UploadType.DeleteItem))
                if (await PerformUpload(item, context))
                    return;

            // ONly do deletes IF there are slots left for uploading
            if (!context.MaxReached)
                foreach (var item in inputSession.Where(item => item.UploadType == UploadType.DeleteItem))
                    if (await PerformUpload(item, context))
                        return;
        }

        private static async Task<bool> PerformUpload(UploadQueueItem item,
            LoadContext context)
        {
            if (context.Increment())
            {
                Console.WriteLine("********** REACHED MAX DailyUploads **********");
                return true;
            }

            if (await UploadOneItem(item))
                RemoveQueuedItem(item);
            return false;
        }

        private static void RemoveQueuedItem(UploadQueueItem updateItem)
        {
            // TODO: Remove the queued item 
            var key = BuildUploadQueueHash(updateItem);


            var filename = BuildQueueItemFileName(key);

            FileHelpers.DeleteFile(filename);
        }

        //[Conditional("SUPPORT_KEYWORDS")]
        private static async Task BuildGalleryItemsForKeywords(Dictionary<string, KeywordEntry> keywords,
            Dictionary<string, GalleryEntry> contents)
        {
            RemoveObeseKeywordEntries(keywords);

            foreach (var keyword in keywords.Values)
            foreach (var sourcePhoto in keyword.Photos)
            {
                var sourcePhotoFullPath = EnsureTerminatedPath("/" + AlbumsRoot + "/" + sourcePhoto.UrlSafePath);
                var sourcePhotoBreadcrumbs =
                    EnsureTerminatedBreadcrumbs("\\" + AlbumsTitle + "\\" + sourcePhoto.BasePath);
                var sourcePhotoPathFragments = sourcePhotoFullPath.Split('/').Where(IsNotEmpty).ToArray();
                var sourcePhotoBreadcrumbFragments =
                    sourcePhotoBreadcrumbs.Split('\\').Where(IsNotEmpty).ToArray();


                var keywordLower =
                    UrlNaming.BuildUrlSafePath(keyword.Keyword.ToLowerInvariant())
                        .TrimEnd("/".ToArray())
                        .TrimStart("-".ToArray())
                        .TrimEnd("-".ToArray());

                var firstKeywordCharLower = keywordLower.Substring(0, 1).ToLowerInvariant();
                var firstKeywordCharUpper = keywordLower.Substring(0, 1).ToUpperInvariant();

                var path =
                    EnsureTerminatedPath("/" + KeywordsRoot + "/" + firstKeywordCharLower + "/" + keywordLower + "/" +
                                         sourcePhotoPathFragments[sourcePhotoPathFragments.Length - 2] + "-" +
                                         sourcePhotoPathFragments.Last());

                var title = sourcePhotoBreadcrumbFragments.Last();
                var parentTitle =
                    sourcePhotoBreadcrumbFragments[sourcePhotoBreadcrumbFragments.Length - 2].ExtractDate(
                        DateFormat.LongDate);
                if (!string.IsNullOrWhiteSpace(parentTitle))
                    title += " (" + parentTitle + ")";

                var breadcrumbs =
                    EnsureTerminatedBreadcrumbs("\\" + KeywordsTitle + "\\" + firstKeywordCharUpper + "\\" +
                                                keyword.Keyword + "\\" +
                                                title);


                var pathFragments = path.Split('/').Where(IsNotEmpty).ToArray();
                var breadcrumbFragments = breadcrumbs.Split('\\').Where(IsNotEmpty).ToArray();

                await EnsureParentFoldersExist(pathFragments, breadcrumbFragments, contents);

                var parentLevel =
                    EnsureTerminatedPath("/" + string.Join("/", pathFragments.Take(pathFragments.Length - 1)));

                Console.WriteLine("Item: {0}", path);

                await AppendVirtualEntry(contents, parentLevel, path, sourcePhotoFullPath,
                    title,
                    sourcePhoto);
            }
        }

        private static void RemoveObeseKeywordEntries(Dictionary<string, KeywordEntry> keywords)
        {
            foreach (var keywordEntry in keywords.Where(entry => entry.Value.Photos.Count > MaxPhotosPerKeyword)
                .ToList())
            {
                Console.WriteLine("Removing over-sized probably generic keyword '{0}'", keywordEntry.Value.Keyword);
                keywords.Remove(keywordEntry.Key);
            }
        }

        private static void AppendKeywordsForLaterProcessing(Photo sourcePhoto,
            Dictionary<string, KeywordEntry> keywords)
        {
            var keywordMetadata =
                sourcePhoto.Metadata.FirstOrDefault(candidate => candidate.Name == MetadataNames.Keywords);
            if (keywordMetadata != null)
                foreach (
                    var keyword in
                    keywordMetadata.Value.Replace(';', ',').Split(',')
                        .Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
                {
                    var safe =
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

        private static GalleryEntry FindParentAlbumPath(Dictionary<string, GalleryEntry> contents, Photo parentRecord)
        {
            var path = parentRecord.BasePath;

            GalleryEntry item;
            if (!contents.TryGetValue(path, out item) || item == null)
                return null;

            return item;
        }

        private static void AddCoordinatesFromChildren(Dictionary<string, GalleryEntry> contents)
        {
            foreach (var entry in contents.Values.Where(candidate => candidate.Location == null))
                if (entry.Children != null && entry.Children.Any())
                {
                    var locations = new List<Location>();

                    AppendChildLocations(entry, locations);

                    var location = LocationHelpers.GetCenterFromDegrees(locations);
                    if (location != null)
                        entry.Location = location;
                }
        }

        private static void AppendChildLocations(GalleryEntry entry, List<Location> locations)
        {
            foreach (var child in entry.Children)
                if (child.Children != null && child.Children.Any())
                    AppendChildLocations(child, locations);
                else if (child.Location != null)
                    locations.Add(child.Location);
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
                return path + terminator;
            return path;
        }

        private static bool IsNotEmpty(string arg)
        {
            return !string.IsNullOrWhiteSpace(arg);
        }

        private static void ProcessSiteIndex(Dictionary<string, GalleryEntry> contents)
        {
            var data = ProduceSiteIndex(contents);

            var outputFilename = Path.Combine(Settings.Default.OutputFolder, "site.js");

            var json = JsonConvert.SerializeObject(data);
            if (!_ignoreExisting && File.Exists(outputFilename))
            {
                Console.WriteLine("Previous Json file exists");
                var originalBytes = FileHelpers.ReadAllBytes(outputFilename);
                var decoded = Encoding.UTF8.GetString(originalBytes);
                if (decoded == json)
                {
                    Console.WriteLine("No changes since last run");
                    return;
                }
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
                    var deletedItems = FindDeletedItems(oldData, data);
                    data.deletedItems.AddRange(deletedItems.OrderBy(x => x));

                    QueueUploadChanges(data, oldData);

                    QueueUploadItemsToDelete(data, deletedItems);
                }
                else
                {
                    QueueUploadAllItems(data);
                }
            }
            else
            {
                QueueUploadAllItems(data);
            }

            ExtensionMethods.RotateLastGenerations(outputFilename);

            var encoded = Encoding.UTF8.GetBytes(json);
            FileHelpers.WriteAllBytes(outputFilename, encoded);
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

            var breadcrumbFragments = parentRecord.Path.Split('/').Where(IsNotEmpty).ToArray();

            for (var folderLevel = 1; folderLevel < breadcrumbFragments.Length; ++folderLevel)
            {
                var level = EnsureTerminatedPath("/" + string.Join("/", breadcrumbFragments.Take(folderLevel)));

                GalleryEntry item;
                if (!contents.TryGetValue(level, out item) || item == null)
                    return new List<GalleryChildItem>();

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

        private static void QueueUploadItemsToDelete(GallerySiteIndex data, List<string> deletedItems)
        {
            foreach (var path in deletedItems)
                QueueUploadOneItem(data, new GalleryItem
                    {
                        Path = path
                    },
                    UploadType.DeleteItem
                );
        }

        private static List<string> FindDeletedItems(GallerySiteIndex oldData, GallerySiteIndex data)
        {
            var oldItems = oldData.items.Select(r => r.Path).ToList();
            var newItems = data.items.Select(r => r.Path).ToList();

            var deletedItems = oldItems.Where(oldItem => !newItems.Contains(oldItem)).ToList();

            if (oldData.deletedItems != null)
                foreach (var oldDeletedItem in oldData.deletedItems)
                    if (!newItems.Contains(oldDeletedItem) && !deletedItems.Contains(oldDeletedItem))
                        deletedItems.Add(oldDeletedItem);
            return deletedItems;
        }

        private static void QueueUploadAllItems(GallerySiteIndex data)
        {
            foreach (
                var item in UploadOrdering(data))
                QueueUploadOneItem(data, item, UploadType.NewItem);
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

        private static void QueueUploadChanges(GallerySiteIndex data, GallerySiteIndex oldData)
        {
            foreach (
                var item in UploadOrdering(data))
            {
                var oldItem = oldData.items.FirstOrDefault(candidate => candidate.Path == item.Path);
                if (oldItem == null || !ItemUpdateHelpers.AreSame(oldItem, item))
                    QueueUploadOneItem(data, item, oldItem == null ? UploadType.NewItem : UploadType.UpdateItem);
            }
        }

        public static void QueueUploadOneItem(GallerySiteIndex data, GalleryItem item, UploadType uploadType)
        {
            var key = BuildUploadQueueHash(item);

            var queueItem = new UploadQueueItem
            {
                Version = data.version,
                Item = item,
                UploadType = uploadType
            };


            var filename = BuildQueueItemFileName(key);

            var json = JsonConvert.SerializeObject(queueItem);

            FileHelpers.WriteAllBytes(filename, Encoding.UTF8.GetBytes(json));
        }

        private static string BuildQueueItemFileName(string key)
        {
            return Path.Combine(Settings.Default.QueueFolder, key + ".queue");
        }

        private static string BuildUploadQueueHash(GalleryItem item)
        {
            return "UploadQueue" + Hasher.HashBytes(Encoding.UTF8.GetBytes(item.Path));
        }

        private static string BuildUploadQueueHash(UploadQueueItem item)
        {
            return BuildUploadQueueHash(item.Item);
        }

        private static async Task<bool> UploadOneItem(UploadQueueItem item)
        {
            var itemToPost = CreateItemToPost(item);

            var progressText = item.Path;

            const int maxRetries = 5;
            var uploaded = false;
            var retry = 0;
            do
            {
                uploaded = await UploadItem(itemToPost, progressText, item.UploadType);
                ++retry;
            } while (!uploaded && retry < maxRetries);
            return uploaded;
        }

        private static async Task<bool> UploadItem(GallerySiteIndex itemToPost, string progressText,
            UploadType uploadType)
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
                        SupportedEncodings = {Encoding.UTF8},
                        Indent = false
                    };

                    var response = await client.PostAsync("tasks/sync", itemToPost, formatter);
                    Console.WriteLine("Status: {0}", response.StatusCode);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine(await response.Content.ReadAsStringAsync());
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
                itemToPost.deletedItems.Add(item.Item.Path);
            else
                itemToPost.items.Add(item.Item);

            return itemToPost;
        }

        private static GalleryChildItem GetNextItem(List<GalleryEntry> siblings, GalleryEntry parentRecord,
            GalleryChildItem lastItem)
        {
            var candidate = siblings.SkipWhile(x => x != parentRecord)
                .Skip(1)
                .Select(CreateGalleryChildItem)
                .FirstOrDefault(item => !IsHiddenItem(item));

            return SkipKnownItem(candidate, lastItem);
        }

        private static GalleryChildItem SkipKnownItem(GalleryChildItem candidate,
            GalleryChildItem itemToIgnoreIfMataches)
        {
            if (candidate != null && candidate.Path == itemToIgnoreIfMataches.Path)
                return null;

            return candidate;
        }

        private static GalleryChildItem GetFirstItem(List<GalleryEntry> siblings, GalleryEntry parentRecord)
        {
            var candidate = siblings
                .Select(CreateGalleryChildItem).FirstOrDefault(item => !IsHiddenItem(item));

            return SkipKnownItem(candidate, parentRecord);
        }

        private static GalleryChildItem SkipKnownItem(GalleryChildItem candidate, GalleryEntry itemToIgnoreIfMataches)
        {
            if (candidate != null && candidate.Path == itemToIgnoreIfMataches.Path)
                return null;

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
            var candidate = siblings
                .Select(CreateGalleryChildItem).LastOrDefault(item => !IsHiddenItem(item));

            return SkipKnownItem(candidate, parentRecord);
        }

        private static GalleryChildItem GetPreviousItem(List<GalleryEntry> siblings, GalleryEntry parentRecord,
            GalleryChildItem firstItem)
        {
            var candidate = siblings.TakeWhile(x => x != parentRecord)
                .Select(CreateGalleryChildItem)
                .LastOrDefault(item => !IsHiddenItem(item));

            return SkipKnownItem(candidate, firstItem);
        }

        public IEnumerable<Tuple<T, T, T>> WithNextAndPrevious<T>(IEnumerable<T> source)
        {
            // Actually yield "the previous two" as well as the current one - this
            // is easier to implement than "previous and next" but they're equivalent
            using (var iterator = source.GetEnumerator())
            {
                if (!iterator.MoveNext())
                    yield break;
                var lastButOne = iterator.Current;
                if (!iterator.MoveNext())
                    yield break;
                var previous = iterator.Current;
                while (iterator.MoveNext())
                {
                    var current = iterator.Current;
                    yield return Tuple.Create(lastButOne, previous, current);
                    lastButOne = previous;
                    previous = current;
                }
            }
        }


        private static List<GalleryEntry> GetSiblings(Dictionary<string, GalleryEntry> contents, GalleryEntry entry)
        {
            if (entry.Path.Length == 1)
                return new List<GalleryEntry>();

            var parentPathIndex = entry.Path.LastIndexOf('/', entry.Path.Length - 2);
            if (parentPathIndex == -1)
                return new List<GalleryEntry>();

            var parentPath = entry.Path.Substring(0, parentPathIndex + 1);

            GalleryEntry parentItem;
            if (contents.TryGetValue(parentPath, out parentItem) && parentItem != null)
                return new List<GalleryEntry>(parentItem.Children.OrderBy(item => item.Path));

            return new List<GalleryEntry>();
        }


        private static async Task AppendPhotoEntry(Dictionary<string, GalleryEntry> contents, string parentLevel,
            string path,
            string title, Photo sourcePhoto)
        {
            DateTime dateCreated;
            DateTime dateUpdated;
            ExtractDates(sourcePhoto, out dateCreated, out dateUpdated);

            var description = ExtractDescription(sourcePhoto);

            var location = ExtractLocation(sourcePhoto);

            var rating = ExtractRating(sourcePhoto);

            var keywords = ExtractKeywords(sourcePhoto);

            if (IsUnderHiddenItem(path))
                keywords = new List<string>();

            await AppendEntry(contents, parentLevel, path, new GalleryEntry
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

        private static async Task AppendVirtualEntry(Dictionary<string, GalleryEntry> contents, string parentLevel,
            string path, string originalPath,
            string title, Photo sourcePhoto)
        {
            DateTime dateCreated;
            DateTime dateUpdated;
            ExtractDates(sourcePhoto, out dateCreated, out dateUpdated);

            var description = ExtractDescription(sourcePhoto);

            var location = ExtractLocation(sourcePhoto);

            var rating = ExtractRating(sourcePhoto);

            var keywords = ExtractKeywords(sourcePhoto);

            await AppendEntry(contents, parentLevel, path, new GalleryEntry
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

        private static async Task AppendVirtualEntryPhotoForGalleryEntry(Dictionary<string, GalleryEntry> contents,
            string parentLevel,
            string path, string originalPath,
            string title, GalleryEntry sourcePhoto)
        {
            var dateCreated = sourcePhoto.DateCreated;
            var dateUpdated = sourcePhoto.DateUpdated;

            var description = sourcePhoto.Description;

            var location = sourcePhoto.Location;

            var rating = sourcePhoto.Rating;

            var keywords = sourcePhoto.Keywords;

            await AppendEntry(contents, parentLevel, path, new GalleryEntry
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
            var kwd = sourcePhoto.Metadata.FirstOrDefault(
                item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Keywords));
            if (kwd != null)
                return kwd.Value.Replace(';', ',').Split(',').Where(IsValidKeywordName).ToList();

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
            var rating = 1;
            var rat = sourcePhoto.Metadata.FirstOrDefault(
                item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Rating));
            if (rat != null)
                if (!int.TryParse(rat.Value, out rating) || rating < 1 || rating > 5)
                    rating = 1;
            return rating;
        }

        private static void ExtractDates(Photo sourcePhoto, out DateTime dateCreated, out DateTime dateUpdated)
        {
            dateCreated = sourcePhoto.Files.Min(file => file.LastModified);
            dateUpdated = sourcePhoto.Files.Max(file => file.LastModified);

            var taken =
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
                        dateCreated = when;

                    if (when > dateUpdated)
                        dateUpdated = when;
                }
            }
        }

        private static string ExtractTitle(Photo sourcePhoto)
        {
            var description = string.Empty;
            var desc =
                sourcePhoto.Metadata.FirstOrDefault(
                    item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Title));
            if (desc != null)
                description = desc.Value;

            return description;
        }

        private static string ExtractDescription(Photo sourcePhoto)
        {
            var description = string.Empty;
            var desc =
                sourcePhoto.Metadata.FirstOrDefault(
                    item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Comment));
            if (desc != null)
                description = desc.Value;
            return description;
        }

        private static Location ExtractLocation(Photo sourcePhoto)
        {
            Location location = null;
            var lat = sourcePhoto.Metadata.FirstOrDefault(
                item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Latitude));
            var lng = sourcePhoto.Metadata.FirstOrDefault(
                item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Longitude));
            if (lat != null && lng != null)
            {
                double latitude;
                double longitude;
                if (double.TryParse(lat.Value, out latitude) && double.TryParse(lng.Value, out longitude))
                    location = new Location
                    {
                        Latitude = latitude,
                        Longitude = longitude
                    };
            }
            return location;
        }

        private static async Task EnsureParentFoldersExist(string[] pathFragments, string[] breadcrumbFragments,
            Dictionary<string, GalleryEntry> contents)
        {
            for (var folderLevel = 1; folderLevel < pathFragments.Length; ++folderLevel)
            {
                var level = EnsureTerminatedPath("/" + string.Join("/", pathFragments.Take(folderLevel)));

                GalleryEntry item;
                if (!contents.TryGetValue(level, out item))
                {
                    var parentLevel =
                        EnsureTerminatedPath("/" + string.Join("/", pathFragments.Take(folderLevel - 1)));

                    await AppendEntry(contents, parentLevel, level, new GalleryEntry
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

        private static async Task AppendEntry(Dictionary<string, GalleryEntry> contents, string parentPath,
            string itemPath,
            GalleryEntry entry)
        {
            await _entrySemaphore.WaitAsync();
            try
            {
                GalleryEntry parent;
                if (!contents.TryGetValue(parentPath, out parent))
                    throw new ApplicationException("Could not find: " + parentPath);

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
            finally
            {
                _entrySemaphore.Release();
            }
        }

        private static async Task AppendRootEntry(Dictionary<string, GalleryEntry> contents)
        {
            {
                var entry = new GalleryEntry
                {
                    Path = "/",
                    OriginalAlbumPath = null,
                    Title = "Mark Ridgwell Photography",
                    Description = "Photos taken by Mark Ridgwell.",
                    Location = null,
                    Children = new List<GalleryEntry>(),
                    DateCreated = DateTime.MaxValue,
                    DateUpdated = DateTime.MinValue
                };

                await _entrySemaphore.WaitAsync();
                try
                {
                    contents.Add("/", entry);
                }
                finally
                {
                    _entrySemaphore.Release();
                }
            }
        }

        private class LoadContext
        {
            private int itemsUploaded;

            public bool MaxReached
            {
                get { return HasMaxBeenReached(itemsUploaded); }
            }

            private bool HasMaxBeenReached(int count)
            {
                return count > _maxDailyUploads;
            }

            public bool Increment()
            {
                var value = Interlocked.Increment(ref itemsUploaded);

                return HasMaxBeenReached(value);
            }
        }

        private class EventDesc
        {
            public string Name { get; set; }

            // /albums/2004/2004-02-01-wadesmill
            public Regex PathMatch { get; set; }

            public string Description { get; set; }
        }

        public class ConverterContractResolver : DefaultContractResolver
        {
            public static readonly ConverterContractResolver Instance = new ConverterContractResolver();

            protected override JsonContract CreateContract(Type objectType)
            {
                var contract = base.CreateContract(objectType);

                // this will only be called once and then cached
                if (objectType == typeof(DateTime) || objectType == typeof(DateTimeOffset))
                    contract.Converter = new JavaScriptDateTimeConverter();

                return contract;
            }
        }
    }
}