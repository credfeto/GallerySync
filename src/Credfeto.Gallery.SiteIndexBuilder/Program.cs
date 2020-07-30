using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.Scanner;
using Credfeto.Gallery.Storage;
using Credfeto.Gallery.Upload;
using Credfeto.Gallery.UploadData;
using Microsoft.Extensions.Configuration;

namespace Credfeto.Gallery.SiteIndexBuilder
{
    internal static class Program
    {
        private const string ALBUMS_ROOT = "albums";
        private const string ALBUMS_TITLE = "Albums";

        private const string KEYWORDS_ROOT = "keywords";
        private const string KEYWORDS_TITLE = "Keywords";

        private const string EVENTS_ROOT = "events";
        private const string EVENTS_TITLE = "Events";

        private const int GALLERY_JSON_VERSION = 1;

        private const int MAX_PHOTOS_PER_KEYWORD = 1000;
        private static int _maxDailyUploads = 8000;

        private static readonly EventDesc[] Events =
        {
            new EventDesc
            {
                Name = "Linkfest",
                PathMatch = new Regex(pattern: @"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(linkfest-harlow)-", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description = "[Linkfest](http://www.linkfestharlow.co.uk/), a free music festival in Harlow Town Park at the bandstand."
            },
            new EventDesc
            {
                Name = "Barleylands - Essex Country Show",
                PathMatch = new Regex(pattern: @"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(barleylands-essex-country-show)-",
                                      RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description = "[Essex Country show](http://www.barleylands.co.uk/essex-country-show) at Barleylands, Billericay."
            },
            new EventDesc
            {
                Name = "Moreton Boxing Day Tug Of War",
                PathMatch = new Regex(pattern: @"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(moreton-boxing-day-tug-of-war)-",
                                      RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description = "The annual tug-of war over the Cripsey Brook at Moreton, Essex."
            },
            new EventDesc
            {
                Name = "Greenwich Tall Ships Festival",
                PathMatch = new Regex(pattern: @"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(greenwich-tall-ships-festival)-",
                                      RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description = ""
            },
            new EventDesc
            {
                Name = "Rock School - Lets Rock The Park",
                PathMatch = new Regex(pattern: @"^/albums/(\d{4})/(\d{4})-(\d{2})-(\d{2})-(rock-school-lets-rock-the-park)-",
                                      RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                Description = ""
            }
        };

        private static bool _ignoreExisting;

        private static readonly SemaphoreSlim EntrySemaphore = new SemaphoreSlim(initialCount: 1);

        private static readonly JsonSerializerOptions SiteIndexSerializer = new JsonSerializerOptions
                                                                            {
                                                                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                                                                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                                                                                IgnoreNullValues = true,
                                                                                WriteIndented = true,
                                                                                PropertyNameCaseInsensitive = true
                                                                            };

        private static async Task<int> Main(string[] args)
        {
            Console.WriteLine(value: "Credfeto.Gallery.SiteIndexBuilder");

            IConfigurationRoot config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                                                                  .AddJsonFile(path: "appsettings.json")
                                                                  .Build();

            Settings.WebServerBaseAddress = config[key: @"WebServerBaseAddress"];
            Settings.QueueFolder = config[key: @"QueueFolder"];
            Settings.OutputFolder = config[key: @"OutputFolder"];
            Settings.DatabaseInputFolder = config[key: @"DatabaseInputFolder"];

            if (args != null)
            {
                if (args.Any(predicate: candidate => StringComparer.InvariantCultureIgnoreCase.Equals(x: candidate, y: "IgnoreExisting")))
                {
                    Console.WriteLine(value: "******* Ignoring existing items *******");
                    _ignoreExisting = true;
                }

                if (args.Any(predicate: candidate => StringComparer.InvariantCultureIgnoreCase.Equals(x: candidate, y: "NoLimit")))
                {
                    Console.WriteLine(value: "******* Ignoring Upload limit *******");
                    _maxDailyUploads = int.MaxValue;
                }
            }

            AlterPriority();

            try
            {
                await ProcessGalleryAsync();

                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine(format: "Error: {0}", arg0: exception.Message);
                Console.WriteLine(format: "Stack Trace: {0}", arg0: exception.StackTrace);

                return 1;
            }
        }

        private static void AlterPriority()
        {
            // TODO: Move to a common Library
            try
            {
                Process.GetCurrentProcess()
                       .PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch
            {
                // Don't care
            }
        }

        private static async Task ProcessGalleryAsync()
        {
            Dictionary<string, GalleryEntry> contents = new Dictionary<string, GalleryEntry>();

            Photo[] target = await LoadRepositoryAsync(Settings.DatabaseInputFolder);

            await AppendRootEntryAsync(contents);

            Dictionary<string, KeywordEntry> keywords = new Dictionary<string, KeywordEntry>();

            foreach (Photo sourcePhoto in target)
            {
                string path = EnsureTerminatedPath("/" + ALBUMS_ROOT + "/" + sourcePhoto.UrlSafePath);
                string breadcrumbs = EnsureTerminatedBreadcrumbs("\\" + ALBUMS_TITLE + "\\" + sourcePhoto.BasePath);
                Console.WriteLine(format: "Item: {0}", arg0: path);

                string[] pathFragments = path.Split(separator: '/')
                                             .Where(IsNotEmpty)
                                             .ToArray();
                string[] breadcrumbFragments = breadcrumbs.Split(separator: '\\')
                                                          .Where(IsNotEmpty)
                                                          .ToArray();

                await EnsureParentFoldersExistAsync(pathFragments: pathFragments, breadcrumbFragments: breadcrumbFragments, contents: contents);

                string parentLevel = EnsureTerminatedPath("/" + string.Join(separator: "/", pathFragments.Take(pathFragments.Length - 1)));

                string title = ExtractTitle(sourcePhoto);

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = breadcrumbFragments[breadcrumbFragments.Length - 1];
                }

                await AppendPhotoEntryAsync(contents: contents, parentLevel: parentLevel, path: path, title: title, sourcePhoto: sourcePhoto);

                if (!IsUnderHiddenItem(path))
                {
                    AppendKeywordsForLaterProcessing(sourcePhoto: sourcePhoto, keywords: keywords);
                }
            }

            Console.WriteLine(format: "Found {0} items total", arg0: contents.Count);
            Console.WriteLine(format: "Found {0} keyword items total", arg0: keywords.Count);

            await Task.WhenAll(BuildEventsAsync(contents), BuildGalleryItemsForKeywordsAsync(keywords: keywords, contents: contents));

            AddCoordinatesFromChildren(contents);
            await ProcessSiteIndexAsync(contents);

            List<UploadQueueItem> queuedItems = await LoadQueuedItemsAsync();
            await UploadQueuedItemsAsync(queuedItems);
        }

        private static async Task<List<UploadQueueItem>> LoadQueuedItemsAsync()
        {
            IEnumerable<string> files = Directory.EnumerateFiles(path: Settings.QueueFolder, searchPattern: "*.queue");

            ConcurrentBag<UploadQueueItem> loaded = new ConcurrentBag<UploadQueueItem>();

            await Task.WhenAll(files.Select(selector: file => LoadOneQueuedFileAsync(file: file, loaded: loaded)));

            Console.WriteLine(format: "Found {0} queued items total", arg0: loaded.Count);

            return loaded.ToList();
        }

        private static async Task LoadOneQueuedFileAsync(string file, ConcurrentBag<UploadQueueItem> loaded)
        {
            byte[] bytes = await FileHelpers.ReadAllBytesAsync(file);

            UploadQueueItem item = JsonSerializer.Deserialize<UploadQueueItem>(Encoding.UTF8.GetString(bytes));

            loaded.Add(item);
        }

        private static async Task<Photo[]> LoadRepositoryAsync(string baseFolder)
        {
            Console.WriteLine(format: "Loading Repository from {0}...", arg0: baseFolder);
            string[] scores = {".info"};

            List<string> sidecarFiles = new List<string>();

            PhotoInfoEmitter emitter = new PhotoInfoEmitter(baseFolder);

            if (Directory.Exists(baseFolder))
            {
                long filesFound = await DirectoryScanner.ScanFolderAsync(baseFolder: baseFolder, fileEmitter: emitter, scores.ToList(), sidecarFiles: sidecarFiles);

                Console.WriteLine(format: "{0} : Files Found: {1}", arg0: baseFolder, arg1: filesFound);
            }

            return emitter.Photos;
        }

        private static async Task BuildEventsAsync(Dictionary<string, GalleryEntry> contents)
        {
            foreach (GalleryEntry folder in contents.Values.Where(UnderAlbumsFolder)
                                                    .Where(HasPhotoChildren)
                                                    .ToList())
            {
                if (IsUnderHiddenItem(folder.Path))
                {
                    continue;
                }

                EventDesc found = null;

                foreach (EventDesc eventEntry in Events)
                {
                    if (eventEntry.PathMatch.IsMatch(folder.Path))
                    {
                        found = eventEntry;

                        break;
                    }
                }

                if (found != null)
                {
                    Console.WriteLine(format: "Found {0} in {1}", arg0: found.Name, arg1: folder.Path);

                    Match pathMatch = found.PathMatch.Match(folder.Path);

                    Group year = pathMatch.Groups[groupnum: 2];
                    Group month = pathMatch.Groups[groupnum: 3];
                    Group day = pathMatch.Groups[groupnum: 4];
                    Group title = pathMatch.Groups[groupnum: 5];

                    Group pathStart = pathMatch.Groups[groupnum: 0];

                    string pathRest = folder.Path.Substring(pathStart.Length)
                                            .Trim()
                                            .TrimEnd(trimChar: '/');

                    if (string.IsNullOrWhiteSpace(pathRest))
                    {
                        pathRest = title.ToString()
                                        .Trim();
                    }

                    string date = year + "-" + month + "-" + day;
                    string titleDate = (date + " MTR").ReformatTitle(DateFormat.LONG_DATE)
                                                      .Replace(oldValue: " - MTR", newValue: string.Empty);

                    foreach (GalleryEntry sourcePhoto in folder.Children.Where(IsImage))
                    {
                        string path = EnsureTerminatedPath(UrlNaming.BuildUrlSafePath("/" + EVENTS_ROOT + "/" + found.Name + "/" + year + "/" + date + "/" + pathRest + "/" + sourcePhoto.Title));
                        string breadcrumbs = EnsureTerminatedBreadcrumbs("\\" + EVENTS_TITLE + "\\" + found.Name + "\\" + year + "\\" + titleDate + "\\" +
                                                                         folder.Title.Replace(titleDate + " - ", newValue: string.Empty) + "\\" + sourcePhoto.Title);

                        string[] pathFragments = path.Split(separator: '/')
                                                     .Where(IsNotEmpty)
                                                     .ToArray();
                        string[] breadcrumbFragments = breadcrumbs.Split(separator: '\\')
                                                                  .Where(IsNotEmpty)
                                                                  .ToArray();

                        await EnsureParentFoldersExistAsync(pathFragments: pathFragments, breadcrumbFragments: breadcrumbFragments, contents: contents);

                        string parentLevel = EnsureTerminatedPath("/" + string.Join(separator: "/", pathFragments.Take(pathFragments.Length - 1)));

                        Console.WriteLine(format: "Item: {0}", arg0: path);

                        await AppendVirtualEntryPhotoForGalleryEntryAsync(contents: contents,
                                                                          parentLevel: parentLevel,
                                                                          path: path,
                                                                          originalPath: sourcePhoto.Path,
                                                                          title: sourcePhoto.Title,
                                                                          sourcePhoto: sourcePhoto);
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
            return HasChildren(item) && item.Children.Any(IsImage);
        }

        private static bool UnderAlbumsFolder(GalleryEntry item)
        {
            return item.Path.StartsWith(value: "/albums/", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImage(GalleryEntry candiate)
        {
            return candiate.ImageSizes != null && candiate.ImageSizes.Any();
        }

        private static async Task UploadQueuedItemsAsync(List<UploadQueueItem> inputSession)
        {
            LoadContext context = new LoadContext();

            // Only upload creates and updates.
            foreach (UploadQueueItem item in inputSession.Where(predicate: item => item.UploadType != UploadType.DELETE_ITEM))
            {
                if (await PerformUploadAsync(item: item, context: context))
                {
                    return;
                }
            }

            // ONly do deletes IF there are slots left for uploading
            if (!context.MaxReached)
            {
                foreach (UploadQueueItem item in inputSession.Where(predicate: item => item.UploadType == UploadType.DELETE_ITEM))
                {
                    if (await PerformUploadAsync(item: item, context: context))
                    {
                        return;
                    }
                }
            }
        }

        private static async Task<bool> PerformUploadAsync(UploadQueueItem item, LoadContext context)
        {
            if (context.Increment())
            {
                Console.WriteLine(value: "********** REACHED MAX DailyUploads **********");

                return true;
            }

            if (await UploadOneItemAsync(item))
            {
                RemoveQueuedItem(item);
            }

            return false;
        }

        private static void RemoveQueuedItem(UploadQueueItem updateItem)
        {
            // TODO: Remove the queued item
            string key = BuildUploadQueueHash(updateItem);

            string filename = BuildQueueItemFileName(key);

            FileHelpers.DeleteFile(filename);
        }

        private static async Task BuildGalleryItemsForKeywordsAsync(Dictionary<string, KeywordEntry> keywords, Dictionary<string, GalleryEntry> contents)
        {
            RemoveObeseKeywordEntries(keywords);

            foreach (KeywordEntry keyword in keywords.Values)
            {
                foreach (Photo sourcePhoto in keyword.Photos)
                {
                    string sourcePhotoFullPath = EnsureTerminatedPath("/" + ALBUMS_ROOT + "/" + sourcePhoto.UrlSafePath);
                    string sourcePhotoBreadcrumbs = EnsureTerminatedBreadcrumbs("\\" + ALBUMS_TITLE + "\\" + sourcePhoto.BasePath);
                    string[] sourcePhotoPathFragments = sourcePhotoFullPath.Split(separator: '/')
                                                                           .Where(IsNotEmpty)
                                                                           .ToArray();
                    string[] sourcePhotoBreadcrumbFragments = sourcePhotoBreadcrumbs.Split(separator: '\\')
                                                                                    .Where(IsNotEmpty)
                                                                                    .ToArray();

                    string keywordLower = UrlNaming.BuildUrlSafePath(keyword.Keyword.ToLowerInvariant())
                                                   .TrimEnd("/".ToArray())
                                                   .TrimStart("-".ToArray())
                                                   .TrimEnd("-".ToArray());

                    string firstKeywordCharLower = keywordLower.Substring(startIndex: 0, length: 1)
                                                               .ToLowerInvariant();
                    string firstKeywordCharUpper = keywordLower.Substring(startIndex: 0, length: 1)
                                                               .ToUpperInvariant();

                    string path = EnsureTerminatedPath("/" + KEYWORDS_ROOT + "/" + firstKeywordCharLower + "/" + keywordLower + "/" + sourcePhotoPathFragments[sourcePhotoPathFragments.Length - 2] +
                                                       "-" + sourcePhotoPathFragments.Last());

                    string title = sourcePhotoBreadcrumbFragments.Last();
                    string parentTitle = sourcePhotoBreadcrumbFragments[sourcePhotoBreadcrumbFragments.Length - 2]
                        .ExtractDate(DateFormat.LONG_DATE);

                    if (!string.IsNullOrWhiteSpace(parentTitle))
                    {
                        title += " (" + parentTitle + ")";
                    }

                    string breadcrumbs = EnsureTerminatedBreadcrumbs("\\" + KEYWORDS_TITLE + "\\" + firstKeywordCharUpper + "\\" + keyword.Keyword + "\\" + title);

                    string[] pathFragments = path.Split(separator: '/')
                                                 .Where(IsNotEmpty)
                                                 .ToArray();
                    string[] breadcrumbFragments = breadcrumbs.Split(separator: '\\')
                                                              .Where(IsNotEmpty)
                                                              .ToArray();

                    await EnsureParentFoldersExistAsync(pathFragments: pathFragments, breadcrumbFragments: breadcrumbFragments, contents: contents);

                    string parentLevel = EnsureTerminatedPath("/" + string.Join(separator: "/", pathFragments.Take(pathFragments.Length - 1)));

                    Console.WriteLine(format: "Item: {0}", arg0: path);

                    await AppendVirtualEntryAsync(contents: contents, parentLevel: parentLevel, path: path, originalPath: sourcePhotoFullPath, title: title, sourcePhoto: sourcePhoto);
                }
            }
        }

        private static void RemoveObeseKeywordEntries(Dictionary<string, KeywordEntry> keywords)
        {
            foreach (KeyValuePair<string, KeywordEntry> keywordEntry in keywords.Where(predicate: entry => entry.Value.Photos.Count > MAX_PHOTOS_PER_KEYWORD)
                                                                                .ToList())
            {
                Console.WriteLine(format: "Removing over-sized probably generic keyword '{0}'", arg0: keywordEntry.Value.Keyword);
                keywords.Remove(keywordEntry.Key);
            }
        }

        private static void AppendKeywordsForLaterProcessing(Photo sourcePhoto, Dictionary<string, KeywordEntry> keywords)
        {
            PhotoMetadata keywordMetadata = sourcePhoto.Metadata.FirstOrDefault(predicate: candidate => candidate.Name == MetadataNames.KEYWORDS);

            if (keywordMetadata != null)
            {
                foreach (string keyword in keywordMetadata.Value.Replace(oldChar: ';', newChar: ',')
                                                          .Split(separator: ',')
                                                          .Where(predicate: candidate => !string.IsNullOrWhiteSpace(candidate)))
                {
                    string safe = UrlNaming.BuildUrlSafePath(keyword.ToLowerInvariant())
                                           .TrimStart("-".ToArray())
                                           .TrimEnd("-".ToArray());

                    if (!keywords.TryGetValue(key: safe, out KeywordEntry entry))
                    {
                        entry = new KeywordEntry(keyword);
                        keywords.Add(key: safe, value: entry);
                    }

                    entry.Photos.Add(sourcePhoto);
                }
            }
        }

        private static void AddCoordinatesFromChildren(Dictionary<string, GalleryEntry> contents)
        {
            foreach (GalleryEntry entry in contents.Values.Where(predicate: candidate => candidate.Location == null))
            {
                if (entry.Children != null && entry.Children.Any())
                {
                    List<Location> locations = new List<Location>();

                    AppendChildLocations(entry: entry, locations: locations);

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
                    AppendChildLocations(entry: child, locations: locations);
                }
                else if (child.Location != null)
                {
                    locations.Add(child.Location);
                }
            }
        }

        private static string EnsureTerminatedPath(string path)
        {
            return EnsureEndsWithSpecificTerminator(path: path, terminator: "/");
        }

        private static string EnsureTerminatedBreadcrumbs(string path)
        {
            return EnsureEndsWithSpecificTerminator(path: path, terminator: "\\");
        }

        private static string EnsureEndsWithSpecificTerminator(string path, string terminator)
        {
            if (!path.EndsWith(value: terminator, comparisonType: StringComparison.Ordinal))
            {
                return path + terminator;
            }

            return path;
        }

        private static bool IsNotEmpty(string arg)
        {
            return !string.IsNullOrWhiteSpace(arg);
        }

        private static async Task ProcessSiteIndexAsync(Dictionary<string, GalleryEntry> contents)
        {
            GallerySiteIndex data = ProduceSiteIndex(contents);

            string outputFilename = Path.Combine(path1: Settings.OutputFolder, path2: "site.js");

            string json = JsonSerializer.Serialize(data);

            if (!_ignoreExisting && File.Exists(outputFilename))
            {
                Console.WriteLine(value: "Previous Json file exists");
                byte[] originalBytes = await FileHelpers.ReadAllBytesAsync(outputFilename);
                string decoded = Encoding.UTF8.GetString(originalBytes);

                if (decoded == json)
                {
                    Console.WriteLine(value: "No changes since last run");

                    return;
                }

                GallerySiteIndex oldData = null;

                try
                {
                    oldData = JsonSerializer.Deserialize<GallerySiteIndex>(json: decoded, options: SiteIndexSerializer);
                }
                catch
                {
                    // don't care
                }

                if (oldData != null)
                {
                    List<string> deletedItems = FindDeletedItems(oldData: oldData, data: data);
                    data.DeletedItems.AddRange(deletedItems.OrderBy(keySelector: x => x));

                    await Task.WhenAll(QueueUploadChangesAsync(data: data, oldData: oldData), QueueUploadItemsToDeleteAsync(data: data, deletedItems: deletedItems));
                }
                else
                {
                    await QueueUploadAllItemsAsync(data);
                }
            }
            else
            {
                await QueueUploadAllItemsAsync(data);
            }

            ExtensionMethods.RotateLastGenerations(outputFilename);

            byte[] encoded = Encoding.UTF8.GetBytes(json);
            await FileHelpers.WriteAllBytesAsync(fileName: outputFilename, bytes: encoded, commit: true);
        }

        private static GallerySiteIndex ProduceSiteIndex(Dictionary<string, GalleryEntry> contents)
        {
            return new GallerySiteIndex
                   {
                       Version = GALLERY_JSON_VERSION,
                       Items = (from parentRecord in contents.Values
                                orderby parentRecord.Path
                                let siblings = GetSiblings(contents: contents, entry: parentRecord)
                                let firstItem = GetFirstItem(siblings: siblings, parentRecord: parentRecord)
                                let lastItem = GetLastItem(siblings: siblings, parentRecord: parentRecord)
                                let previousItem = GetPreviousItem(siblings: siblings, parentRecord: parentRecord, firstItem: firstItem)
                                let nextItem = GetNextItem(siblings: siblings, parentRecord: parentRecord, lastItem: lastItem)
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
                                           Children = (from childRecord in parentRecord.Children where !IsHiddenItem(childRecord) orderby childRecord.Path select CreateGalleryChildItem(childRecord))
                                               .ToList(),
                                           Breadcrumbs = ExtractItemPreadcrumbs(contents: contents, parentRecord: parentRecord)
                                       }).ToList(),
                       DeletedItems = new List<string>()
                   };
        }

        private static List<GalleryChildItem> ExtractItemPreadcrumbs(Dictionary<string, GalleryEntry> contents, GalleryEntry parentRecord)
        {
            List<GalleryChildItem> items = new List<GalleryChildItem>();

            string[] breadcrumbFragments = parentRecord.Path.Split(separator: '/')
                                                       .Where(IsNotEmpty)
                                                       .ToArray();

            for (int folderLevel = 1; folderLevel < breadcrumbFragments.Length; ++folderLevel)
            {
                string level = EnsureTerminatedPath("/" + string.Join(separator: "/", breadcrumbFragments.Take(folderLevel)));

                if (!contents.TryGetValue(key: level, out GalleryEntry item) || item == null)
                {
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
            return path.StartsWith(value: "/albums/private/", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        private static Task QueueUploadItemsToDeleteAsync(GallerySiteIndex data, List<string> deletedItems)
        {
            return Task.WhenAll(deletedItems.Select(selector: path => QueueUploadOneItemAsync(data: data, new GalleryItem {Path = path}, uploadType: UploadType.DELETE_ITEM)));
        }

        private static List<string> FindDeletedItems(GallerySiteIndex oldData, GallerySiteIndex data)
        {
            List<string> oldItems = oldData.Items.Select(selector: r => r.Path)
                                           .ToList();
            List<string> newItems = data.Items.Select(selector: r => r.Path)
                                        .ToList();

            List<string> deletedItems = oldItems.Where(predicate: oldItem => !newItems.Contains(oldItem))
                                                .ToList();

            if (oldData.DeletedItems != null)
            {
                foreach (string oldDeletedItem in oldData.DeletedItems)
                {
                    if (!newItems.Contains(oldDeletedItem) && !deletedItems.Contains(oldDeletedItem))
                    {
                        deletedItems.Add(oldDeletedItem);
                    }
                }
            }

            return deletedItems;
        }

        private static Task QueueUploadAllItemsAsync(GallerySiteIndex data)
        {
            return Task.WhenAll(UploadOrdering(data)
                                    .Select(selector: item => QueueUploadOneItemAsync(data: data, item: item, uploadType: UploadType.NEW_ITEM)));
        }

        private static IOrderedEnumerable<GalleryItem> UploadOrdering(GallerySiteIndex data)
        {
            return data.Items.OrderBy(StrictTypeOrdering)
                       .ThenBy(keySelector: candidate => candidate.Path);
        }

        private static int StrictTypeOrdering(GalleryItem candidate)
        {
            // Photos first... albums after
            return candidate.Type == "photo" ? 1 : 2;
        }

        private static Task QueueUploadChangesAsync(GallerySiteIndex data, GallerySiteIndex oldData)
        {
            return Task.WhenAll(UploadOrdering(data)
                                    .Select(selector: item => QueueOneNewOrModifiedItemAsync(data: data, oldData: oldData, item: item)));
        }

        private static async Task QueueOneNewOrModifiedItemAsync(GallerySiteIndex data, GallerySiteIndex oldData, GalleryItem item)
        {
            GalleryItem oldItem = oldData.Items.FirstOrDefault(predicate: candidate => candidate.Path == item.Path);

            if (oldItem == null || !ItemUpdateHelpers.AreSame(oldItem: oldItem, other: item))
            {
                await QueueUploadOneItemAsync(data: data, item: item, oldItem == null ? UploadType.NEW_ITEM : UploadType.UPDATE_ITEM);
            }
        }

        private static Task QueueUploadOneItemAsync(GallerySiteIndex data, GalleryItem item, UploadType uploadType)
        {
            string key = BuildUploadQueueHash(item);

            UploadQueueItem queueItem = new UploadQueueItem {Version = data.Version, Item = item, UploadType = uploadType};

            string filename = BuildQueueItemFileName(key);

            string json = JsonSerializer.Serialize(value: queueItem, options: SiteIndexSerializer);

            return FileHelpers.WriteAllBytesAsync(fileName: filename, Encoding.UTF8.GetBytes(json), commit: true);
        }

        private static string BuildQueueItemFileName(string key)
        {
            return Path.Combine(path1: Settings.QueueFolder, key + ".queue");
        }

        private static string BuildUploadQueueHash(GalleryItem item)
        {
            return "UploadQueue" + Hasher.HashBytes(Encoding.UTF8.GetBytes(item.Path));
        }

        private static string BuildUploadQueueHash(UploadQueueItem item)
        {
            return BuildUploadQueueHash(item.Item);
        }

        private static async Task<bool> UploadOneItemAsync(UploadQueueItem item)
        {
            // TODO - move the intialization etc somewhere else.
            UploadHelper uh = new UploadHelper(new Uri(Settings.WebServerBaseAddress));

            GallerySiteIndex itemToPost = CreateItemToPost(item);

            string progressText = item.Path;

            const int maxRetries = 5;
            bool uploaded;
            int retry = 0;

            do
            {
                uploaded = await uh.UploadItemAsync(itemToPost: itemToPost, progressText: progressText, uploadType: item.UploadType);
                ++retry;
            } while (!uploaded && retry < maxRetries);

            return uploaded;
        }

        private static GallerySiteIndex CreateItemToPost(UploadQueueItem item)
        {
            GallerySiteIndex itemToPost = new GallerySiteIndex {Version = item.Version, Items = new List<GalleryItem>(), DeletedItems = new List<string>()};

            if (item.UploadType == UploadType.DELETE_ITEM)
            {
                itemToPost.DeletedItems.Add(item.Item.Path);
            }
            else
            {
                itemToPost.Items.Add(item.Item);
            }

            return itemToPost;
        }

        private static GalleryChildItem GetNextItem(List<GalleryEntry> siblings, GalleryEntry parentRecord, GalleryChildItem lastItem)
        {
            GalleryChildItem candidate = siblings.SkipWhile(predicate: x => x != parentRecord)
                                                 .Skip(count: 1)
                                                 .Select(CreateGalleryChildItem)
                                                 .FirstOrDefault(predicate: item => !IsHiddenItem(item));

            return SkipKnownItem(candidate: candidate, itemToIgnoreIfMataches: lastItem);
        }

        private static GalleryChildItem SkipKnownItem(GalleryChildItem candidate, GalleryChildItem itemToIgnoreIfMataches)
        {
            if (candidate != null && candidate.Path == itemToIgnoreIfMataches.Path)
            {
                return null;
            }

            return candidate;
        }

        private static GalleryChildItem GetFirstItem(List<GalleryEntry> siblings, GalleryEntry parentRecord)
        {
            GalleryChildItem candidate = siblings.Select(CreateGalleryChildItem)
                                                 .FirstOrDefault(predicate: item => !IsHiddenItem(item));

            return SkipKnownItem(candidate: candidate, itemToIgnoreIfMataches: parentRecord);
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
            GalleryChildItem candidate = siblings.Select(CreateGalleryChildItem)
                                                 .LastOrDefault(predicate: item => !IsHiddenItem(item));

            return SkipKnownItem(candidate: candidate, itemToIgnoreIfMataches: parentRecord);
        }

        private static GalleryChildItem GetPreviousItem(List<GalleryEntry> siblings, GalleryEntry parentRecord, GalleryChildItem firstItem)
        {
            GalleryChildItem candidate = siblings.TakeWhile(predicate: x => x != parentRecord)
                                                 .Select(CreateGalleryChildItem)
                                                 .LastOrDefault(predicate: item => !IsHiddenItem(item));

            return SkipKnownItem(candidate: candidate, itemToIgnoreIfMataches: firstItem);
        }

        private static List<GalleryEntry> GetSiblings(Dictionary<string, GalleryEntry> contents, GalleryEntry entry)
        {
            if (entry.Path.Length == 1)
            {
                return new List<GalleryEntry>();
            }

            int parentPathIndex = entry.Path.LastIndexOf(value: '/', entry.Path.Length - 2);

            if (parentPathIndex == -1)
            {
                return new List<GalleryEntry>();
            }

            string parentPath = entry.Path.Substring(startIndex: 0, parentPathIndex + 1);

            if (contents.TryGetValue(key: parentPath, out GalleryEntry parentItem) && parentItem != null)
            {
                return new List<GalleryEntry>(parentItem.Children.OrderBy(keySelector: item => item.Path));
            }

            return new List<GalleryEntry>();
        }

        private static Task AppendPhotoEntryAsync(Dictionary<string, GalleryEntry> contents, string parentLevel, string path, string title, Photo sourcePhoto)
        {
            ExtractDates(sourcePhoto: sourcePhoto, out DateTime dateCreated, out DateTime dateUpdated);

            string description = ExtractDescription(sourcePhoto);

            Location location = ExtractLocation(sourcePhoto);

            int rating = ExtractRating(sourcePhoto);

            List<string> keywords = ExtractKeywords(sourcePhoto);

            if (IsUnderHiddenItem(path))
            {
                keywords = new List<string>();
            }

            return AppendEntryAsync(contents: contents,
                                    parentPath: parentLevel,
                                    itemPath: path,
                                    new GalleryEntry
                                    {
                                        Path = path,
                                        OriginalAlbumPath = null,
                                        Title = title,
                                        Description = description,
                                        Children = new List<GalleryEntry>(),
                                        Location = location,
                                        ImageSizes = sourcePhoto.ImageSizes,
                                        Rating = rating,
                                        Metadata = sourcePhoto.Metadata.Where(IsPublishableMetadata)
                                                              .OrderBy(keySelector: item => item.Name.ToLowerInvariant())
                                                              .ToList(),
                                        Keywords = keywords,
                                        DateCreated = dateCreated,
                                        DateUpdated = dateUpdated
                                    });
        }

        private static Task AppendVirtualEntryAsync(Dictionary<string, GalleryEntry> contents, string parentLevel, string path, string originalPath, string title, Photo sourcePhoto)
        {
            ExtractDates(sourcePhoto: sourcePhoto, out DateTime dateCreated, out DateTime dateUpdated);

            string description = ExtractDescription(sourcePhoto);

            Location location = ExtractLocation(sourcePhoto);

            int rating = ExtractRating(sourcePhoto);

            List<string> keywords = ExtractKeywords(sourcePhoto);

            return AppendEntryAsync(contents: contents,
                                    parentPath: parentLevel,
                                    itemPath: path,
                                    new GalleryEntry
                                    {
                                        Path = path,
                                        OriginalAlbumPath = originalPath,
                                        Title = title,
                                        Description = description,
                                        Children = new List<GalleryEntry>(),
                                        Location = location,
                                        ImageSizes = sourcePhoto.ImageSizes,
                                        Rating = rating,
                                        Metadata = sourcePhoto.Metadata.Where(IsPublishableMetadata)
                                                              .OrderBy(keySelector: item => item.Name.ToLowerInvariant())
                                                              .ToList(),
                                        Keywords = keywords,
                                        DateCreated = dateCreated,
                                        DateUpdated = dateUpdated
                                    });
        }

        private static Task AppendVirtualEntryPhotoForGalleryEntryAsync(Dictionary<string, GalleryEntry> contents,
                                                                        string parentLevel,
                                                                        string path,
                                                                        string originalPath,
                                                                        string title,
                                                                        GalleryEntry sourcePhoto)
        {
            DateTime dateCreated = sourcePhoto.DateCreated;
            DateTime dateUpdated = sourcePhoto.DateUpdated;

            string description = sourcePhoto.Description;

            Location location = sourcePhoto.Location;

            int rating = sourcePhoto.Rating;

            List<string> keywords = sourcePhoto.Keywords;

            return AppendEntryAsync(contents: contents,
                                    parentPath: parentLevel,
                                    itemPath: path,
                                    new GalleryEntry
                                    {
                                        Path = path,
                                        OriginalAlbumPath = originalPath,
                                        Title = title,
                                        Description = description,
                                        Children = new List<GalleryEntry>(),
                                        Location = location,
                                        ImageSizes = sourcePhoto.ImageSizes,
                                        Rating = rating,
                                        Metadata = sourcePhoto.Metadata.Where(IsPublishableMetadata)
                                                              .OrderBy(keySelector: item => item.Name.ToLowerInvariant())
                                                              .ToList(),
                                        Keywords = keywords,
                                        DateCreated = dateCreated,
                                        DateUpdated = dateUpdated
                                    });
        }

        private static List<string> ExtractKeywords(Photo sourcePhoto)
        {
            PhotoMetadata kwd = sourcePhoto.Metadata.FirstOrDefault(predicate: item => StringComparer.InvariantCultureIgnoreCase.Equals(x: item.Name, y: MetadataNames.KEYWORDS));

            if (kwd != null)
            {
                return kwd.Value.Replace(oldChar: ';', newChar: ',')
                          .Split(separator: ',')
                          .Where(IsValidKeywordName)
                          .ToList();
            }

            return new List<string>();
        }

        private static bool IsValidKeywordName(string s)
        {
            return !string.IsNullOrWhiteSpace(s);
        }

        private static bool IsPublishableMetadata(PhotoMetadata metadata)
        {
            string[] notPublishable =
            {
                MetadataNames.TITLE, MetadataNames.DATE_TAKEN, MetadataNames.KEYWORDS, MetadataNames.RATING, MetadataNames.LATITUDE, MetadataNames.LONGITUDE, MetadataNames.COMMENT
            };

            return notPublishable.All(predicate: item => !StringComparer.InvariantCultureIgnoreCase.Equals(x: item, y: metadata.Name));
        }

        private static int ExtractRating(Photo sourcePhoto)
        {
            int rating = 1;
            PhotoMetadata rat = sourcePhoto.Metadata.FirstOrDefault(predicate: item => StringComparer.InvariantCultureIgnoreCase.Equals(x: item.Name, y: MetadataNames.RATING));

            if (rat != null)
            {
                if (!int.TryParse(s: rat.Value, result: out rating) || rating < 1 || rating > 5)
                {
                    rating = 1;
                }
            }

            return rating;
        }

        private static void ExtractDates(Photo sourcePhoto, out DateTime dateCreated, out DateTime dateUpdated)
        {
            dateCreated = sourcePhoto.Files.Min(selector: file => file.LastModified);
            dateUpdated = sourcePhoto.Files.Max(selector: file => file.LastModified);

            PhotoMetadata taken = sourcePhoto.Metadata.FirstOrDefault(predicate: item => StringComparer.InvariantCultureIgnoreCase.Equals(x: item.Name, y: MetadataNames.DATE_TAKEN));

            if (taken != null)
            {
                // Extract the date from the value;

                if (DateTime.TryParse(s: taken.Value, provider: CultureInfo.InvariantCulture, styles: DateTimeStyles.AllowWhiteSpaces, out DateTime when))
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
            PhotoMetadata desc = sourcePhoto.Metadata.FirstOrDefault(predicate: item => StringComparer.InvariantCultureIgnoreCase.Equals(x: item.Name, y: MetadataNames.TITLE));

            if (desc != null)
            {
                description = desc.Value;
            }

            return description;
        }

        private static string ExtractDescription(Photo sourcePhoto)
        {
            string description = string.Empty;
            PhotoMetadata desc = sourcePhoto.Metadata.FirstOrDefault(predicate: item => StringComparer.InvariantCultureIgnoreCase.Equals(x: item.Name, y: MetadataNames.COMMENT));

            if (desc != null)
            {
                description = desc.Value;
            }

            return description;
        }

        private static Location ExtractLocation(Photo sourcePhoto)
        {
            Location location = null;
            PhotoMetadata lat = sourcePhoto.Metadata.FirstOrDefault(predicate: item => StringComparer.InvariantCultureIgnoreCase.Equals(x: item.Name, y: MetadataNames.LATITUDE));
            PhotoMetadata lng = sourcePhoto.Metadata.FirstOrDefault(predicate: item => StringComparer.InvariantCultureIgnoreCase.Equals(x: item.Name, y: MetadataNames.LONGITUDE));

            if (lat != null && lng != null)
            {
                if (double.TryParse(s: lat.Value, out double latitude) && double.TryParse(s: lng.Value, out double longitude))
                {
                    location = new Location {Latitude = latitude, Longitude = longitude};
                }
            }

            return location;
        }

        private static async Task EnsureParentFoldersExistAsync(string[] pathFragments, string[] breadcrumbFragments, Dictionary<string, GalleryEntry> contents)
        {
            for (int folderLevel = 1; folderLevel < pathFragments.Length; ++folderLevel)
            {
                string level = EnsureTerminatedPath("/" + string.Join(separator: "/", pathFragments.Take(folderLevel)));

                if (!contents.TryGetValue(key: level, out GalleryEntry _))
                {
                    string parentLevel = EnsureTerminatedPath("/" + string.Join(separator: "/", pathFragments.Take(folderLevel - 1)));

                    await AppendEntryAsync(contents: contents,
                                           parentPath: parentLevel,
                                           itemPath: level,
                                           new GalleryEntry
                                           {
                                               Path = level,
                                               OriginalAlbumPath = null,
                                               Title = breadcrumbFragments[folderLevel - 1]
                                                   .ReformatTitle(DateFormat.LONG_DATE),
                                               Description = string.Empty,
                                               Location = null,
                                               Children = new List<GalleryEntry>(),
                                               DateCreated = DateTime.MaxValue,
                                               DateUpdated = DateTime.MinValue
                                           });
                }
            }
        }

        private static async Task AppendEntryAsync(Dictionary<string, GalleryEntry> contents, string parentPath, string itemPath, GalleryEntry entry)
        {
            await EntrySemaphore.WaitAsync();

            try
            {
                if (!contents.TryGetValue(key: parentPath, out GalleryEntry parent))
                {
                    throw new FileContentException("Could not find: " + parentPath);
                }

                if (contents.TryGetValue(key: itemPath, out GalleryEntry _))
                {
                    // This shouldn't ever happen, but wth, it does!
                    Console.WriteLine(format: "ERROR: DUPLICATE PATH: {0}", arg0: itemPath);

                    return;
                }

                Console.WriteLine(format: " * Path: {0}", arg0: itemPath);
                Console.WriteLine(format: "   + Title: {0}", arg0: entry.Title);
                parent.Children.Add(entry);

                contents.Add(key: itemPath, value: entry);
            }
            finally
            {
                EntrySemaphore.Release();
            }
        }

        private static async Task AppendRootEntryAsync(Dictionary<string, GalleryEntry> contents)
        {
            GalleryEntry entry = new GalleryEntry
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

            await EntrySemaphore.WaitAsync();

            try
            {
                contents.Add(key: "/", value: entry);
            }
            finally
            {
                EntrySemaphore.Release();
            }
        }

        private sealed class LoadContext
        {
            private int _itemsUploaded;

            public bool MaxReached => HasMaxBeenReached(this._itemsUploaded);

            private static bool HasMaxBeenReached(int count)
            {
                return count > _maxDailyUploads;
            }

            public bool Increment()
            {
                int value = Interlocked.Increment(ref this._itemsUploaded);

                return HasMaxBeenReached(value);
            }
        }

        private sealed class EventDesc
        {
            public string Name { get; set; }

            // /albums/2004/2004-02-01-wadesmill
            public Regex PathMatch { get; set; }

            public string Description { get; set; }
        }

        // public class ConverterContractResolver : DefaultContractResolver
        // {
        //     public static readonly ConverterContractResolver Instance = new ConverterContractResolver();
        //
        //     protected override JsonContract CreateContract(Type objectType)
        //     {
        //         JsonContract contract = base.CreateContract(objectType);
        //
        //         // this will only be called once and then cached
        //         if (objectType == typeof(DateTime) || objectType == typeof(DateTimeOffset))
        //         {
        //             contract.Converter = new JavaScriptDateTimeConverter();
        //         }
        //
        //         return contract;
        //     }
        // }
    }
}