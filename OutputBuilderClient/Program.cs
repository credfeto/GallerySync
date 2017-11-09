using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using FileNaming;
using Newtonsoft.Json;
using OutputBuilderClient.Properties;
using StorageHelpers;
using Twaddle.Directory.Scanner;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    internal class Program
    {
        private static readonly SemaphoreSlim _sempahore = new SemaphoreSlim(1);

        public static async Task<bool> MetadataVersionRequiresRebuild(Photo targetPhoto)
        {
            if (MetadataVersionHelpers.RequiresRebuild(targetPhoto.Version))
            {
                await ConsoleOutput.Line(
                    " +++ Metadata update: Metadata version Requires rebuild. (Current: " + targetPhoto.Version
                    + " Expected: " + Constants.CurrentMetadataVersion + ")");
                return true;
            }

            return false;
        }

        private static void AddUploadFiles(List<string> filesCreated)
        {
            // TODO: Ire-implement
//            foreach (string file in filesCreated)
//            {
//                string key = "U" + Hasher.HashBytes(Encoding.UTF8.GetBytes(file));
//
//                var existing = outputSession.Load<FileToUpload>(key);
//                if (existing == null)
//                {
//                    var fileToUpload = new FileToUpload { FileName = file, Completed = false };
//
//                    outputSession.Store(fileToUpload, key);
//                }
//                else
//                {
//                    if (existing.Completed)
//                    {
//                        existing.Completed = false;
//                        outputSession.Store(existing, key);
//                    }
//                }
//            }
        }

        private static void ForceGarbageCollection()
        {
            GC.GetTotalMemory(true);
        }

        private static Task KillDeadItems(HashSet<string> liveItems)
        {
            return Task.CompletedTask;

            // TODO: REIMPLEMENT
//            using (IDocumentSession outputSession = documentStoreOutput.OpenSession())
//            {
//                foreach (Photo sourcePhoto in outputSession.GetAll<Photo>())
//                {
//                    if (liveItems.Contains(sourcePhoto.PathHash))
//                    {
//                        continue;
//                    }
//
//                    KillOnePhoto(documentStoreOutput, sourcePhoto);
//                }
//            }
        }

        private static void KillOnePhoto(Photo sourcePhoto)
        {
            // TODO: REIMPLEMENT
//            using (IDocumentSession deletionSession = documentStoreOutput.OpenSession())
//            {
//                var targetPhoto = deletionSession.Load<Photo>(sourcePhoto.PathHash);
//                if (targetPhoto != null)
//                {
//                    OutputText("Deleting {0} as no longer exists", sourcePhoto.UrlSafePath);
//                    deletionSession.Delete(targetPhoto);
//
//                    deletionSession.SaveChanges();
//                }
//                else
//                {
//                    OutputText("Could not delete {0}", sourcePhoto.UrlSafePath);
//                }
//            }
        }

        private static int Main(string[] args)
        {
            Console.WriteLine("OutputBuilderClient");

            //BoostPriority();

            return AsyncMain(args).GetAwaiter().GetResult();
        }

        private static async Task<int> AsyncMain(string[] args)
        {
            if (args.Length == 1)
            {
                ShortUrls.Load();

                try
                {
                    await ReadMetadata(args[0]);

                    return 0;
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Error: {0}", exception.Message);
                    Console.WriteLine("Stack Trace: {0}", exception.StackTrace);
                    return 1;
                }
            }

            int retval;
            try
            {
                ShortUrls.Load();

                await ProcessGallery();


                retval = 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: {0}", exception.Message);
                Console.WriteLine("Stack Trace: {0}", exception.StackTrace);

                retval = 1;
            }

            await DumpBrokenImages();

            return retval;
        }

        private static Task DumpBrokenImages()
        {
            var images = BrokenImages.AllBrokenImages();

            File.WriteAllLines(Settings.Default.BrokenImagesFile, images, Encoding.UTF8);

            return ConsoleOutput.Line("Broken Images: {0}", images.Length);
        }

        private static async Task<HashSet<string>> Process(
            Photo[] source,
            Photo[] target)
        {
            var items = new ConcurrentDictionary<string, bool>();

            await Task.WhenAll(
                source.Select(
                    sourcePhoto => ProcessSinglePhoto(target, sourcePhoto, items)
                ).ToArray());


            return new HashSet<string>(items.Keys);
        }

        private static async Task ProcessGallery()
        {
            var sourceTask = RepositoryLoader.LoadEmptyRepository(Settings.Default.RootFolder);
            var targetTask = RepositoryLoader.LoadRepository(Settings.Default.DatabaseOutputFolder);

            await Task.WhenAll(sourceTask, targetTask);

            var source = sourceTask.Result;
            var target = targetTask.Result;

            var liveItems = await Process(source, target);

            await KillDeadItems(liveItems);
        }

        private static async Task ProcessOneFile(
            Photo sourcePhoto,
            Photo targetPhoto,
            bool rebuild,
            bool rebuildMetadata,
            string url,
            string shortUrl)
        {
            await ConsoleOutput.Line(rebuild ? "Rebuild: {0}" : "Build: {0}", sourcePhoto.UrlSafePath);

            await targetPhoto.UpdateFileHashes(sourcePhoto);

            var buildMetadata = targetPhoto == null || rebuild || rebuildMetadata
                                || targetPhoto != null && targetPhoto.Metadata == null;

            if (buildMetadata)
                sourcePhoto.Metadata = MetadataExtraction.ExtractMetadata(sourcePhoto);
            else
                sourcePhoto.Metadata = targetPhoto.Metadata;

            var buildImages = targetPhoto == null || rebuild
                              || targetPhoto != null && !targetPhoto.ImageSizes.HasAny();

            var filesCreated = new List<string>();
            if (buildImages)
            {
                var creationDate = MetadataHelpers.ExtractCreationDate(sourcePhoto.Metadata);
                sourcePhoto.ImageSizes = await ImageExtraction.BuildImages(
                    sourcePhoto,
                    filesCreated,
                    creationDate,
                    url,
                    shortUrl);
            }
            else
            {
                sourcePhoto.ImageSizes = targetPhoto.ImageSizes;
            }

            sourcePhoto.Version = Constants.CurrentMetadataVersion;

            if (targetPhoto != null)
            {
                targetPhoto.UpdateTargetWithSourceProperties(sourcePhoto);
                targetPhoto.Version = Constants.CurrentMetadataVersion;

                if (buildImages)
                    AddUploadFiles(filesCreated);

                await Store(targetPhoto);
            }
            else
            {
                AddUploadFiles(filesCreated);

                await Store(sourcePhoto);
            }
        }

        private static Task Store(Photo photo)
        {
            var safeUrl = photo.UrlSafePath.Replace('/', Path.DirectorySeparatorChar);
            safeUrl = safeUrl.TrimEnd(Path.DirectorySeparatorChar);
            safeUrl += ".info";

            var outputPath = Path.Combine(
                Settings.Default.DatabaseOutputFolder,
                safeUrl
            );

            var txt = JsonConvert.SerializeObject(photo);
            return FileHelpers.WriteAllBytes(outputPath, Encoding.UTF8.GetBytes(txt));
        }

        private static async Task ProcessSinglePhoto(
            Photo[] target,
            Photo sourcePhoto,
            ConcurrentDictionary<string, bool> items)
        {
            ForceGarbageCollection();

            try
            {
                var targetPhoto =
                    target.FirstOrDefault(item =>
                        item.PathHash == sourcePhoto.PathHash);
                var build = targetPhoto == null;
                var rebuild = targetPhoto != null &&
                              await RebuildDetection.NeedsFullResizedImageRebuild(sourcePhoto, targetPhoto);
                var rebuildMetadata =
                    targetPhoto != null && await RebuildDetection.MetadataVersionOutOfDate(targetPhoto);

                var url = "https://www.markridgwell.co.uk/albums/" + sourcePhoto.UrlSafePath;
                string shortUrl;

                if (targetPhoto != null)
                {
                    shortUrl = targetPhoto.ShortUrl;

                    if (ShortUrls.ShouldGenerateShortUrl(sourcePhoto, shortUrl, url))
                    {
                        shortUrl = await TryGenerateShortUrl(url);

                        if (!StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                        {
                            ShortUrls.LogShortUrl(url, shortUrl);

                            rebuild = true;
                            await ConsoleOutput.Line(
                                " +++ Force rebuild: missing shortcut URL.  New short url: {0}",
                                shortUrl);
                        }
                    }
                }
                else
                {
                    if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                        await ConsoleOutput.Line("* Reusing existing short url: {0}", shortUrl);
                }

                if (!string.IsNullOrWhiteSpace(shortUrl)
                    && !StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                    sourcePhoto.ShortUrl = shortUrl;
                else
                    shortUrl = Constants.DefaultShortUrl;

                if (build || rebuild || rebuildMetadata)
                    await ProcessOneFile(sourcePhoto, targetPhoto, rebuild, rebuildMetadata, url, shortUrl);
                else
                    await ConsoleOutput.Line("Unchanged: {0}", targetPhoto.UrlSafePath);

                items.TryAdd(sourcePhoto.PathHash, true);
            }
            catch (AbortProcessingException exception)
            {
                BrokenImages.LogBrokenImage(sourcePhoto.UrlSafePath, exception.Message);
                throw;
            }
            catch (Exception exception)
            {
                BrokenImages.LogBrokenImage(sourcePhoto.UrlSafePath, exception.Message);
            }
        }

        private static async Task ReadMetadata(string filename)
        {
            var folder = Path.GetDirectoryName(filename);
            var file = Path.GetFileName(filename);
            var extension = Path.GetExtension(filename);

            var fileGroup = new List<string>();
            if (File.Exists(filename.Replace(extension, ".xmp")))
                fileGroup.Add(file.Replace(extension, ".xmp"));

            var entry = new FileEntry
            {
                Folder = folder,
                RelativeFolder = folder.Substring(Settings.Default.RootFolder.Length + 1),
                LocalFileName = file,
                AlternateFileNames = fileGroup
            };

            var basePath = Path.Combine(
                entry.RelativeFolder,
                Path.GetFileNameWithoutExtension(entry.LocalFileName));

            var urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            var photo = new Photo
            {
                BasePath = basePath,
                UrlSafePath = urlSafePath,
                PathHash = Hasher.HashBytes(Encoding.UTF8.GetBytes(urlSafePath)),
                ImageExtension = Path.GetExtension(entry.LocalFileName),
                Files =
                    fileGroup.Select(
                        x =>
                            new ComponentFile
                            {
                                Extension =
                                    Path.GetExtension(x)
                                        .TrimStart('.'),
                                Hash = string.Empty,
                                LastModified = new DateTime(2014, 1, 1),
                                FileSize = 1000
                            }).ToList()
            };

            var metadata = MetadataExtraction.ExtractMetadata(photo);
            foreach (var item in metadata)
                await ConsoleOutput.Line("{0} = {1}", item.Name, item.Value);
        }

        private static async Task<string> TryGenerateShortUrl(string url)
        {
            string shortUrl;
            if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                return shortUrl;


            await _sempahore.WaitAsync();

            if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                return shortUrl;

            try
            {
                var filename = Settings.Default.ShortNamesFile + ".tracking.json";

                var tracking = new List<ShortenerCount>();
                if (File.Exists(filename))
                {
                    var bytes = await FileHelpers.ReadAllBytes(filename);

                    var items = JsonConvert.DeserializeObject<ShortenerCount[]>(Encoding.UTF8.GetString(bytes));

                    tracking.AddRange(items);
                }

                const int maxImpressionsPerMonth = 100;

                var now = DateTime.UtcNow;

                var counter = tracking.FirstOrDefault(item => item.Year == now.Year && item.Month == now.Month);
                if (counter == null)
                {
                    counter = new ShortenerCount();

                    var totalImpressionsEver = 0L;
                    foreach (var month in tracking)
                        totalImpressionsEver += month.Impressions;

                    counter.Year = now.Year;
                    counter.Month = now.Month;
                    counter.Impressions = 1;
                    counter.TotalImpressionsEver = totalImpressionsEver;

                    tracking.Add(counter);

                    await FileHelpers.WriteAllBytes(
                        filename,
                        Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(tracking.ToArray())));
                }
                else
                {
                    if (counter.Impressions < maxImpressionsPerMonth)
                    {
                        Console.WriteLine("Bitly Impressions for {0}", counter.Impressions);
                        Console.WriteLine("Bitly Impressions total {0}", counter.TotalImpressionsEver);
                        ++counter.Impressions;
                        ++counter.TotalImpressionsEver;

                        await FileHelpers.WriteAllBytes(
                            filename,
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(tracking.ToArray())));
                    }
                }

                if (counter.Impressions < maxImpressionsPerMonth)
                {
                    var shortened = await BitlyUrlShortner.Shorten(new Uri(url));

                    return shortened.ToString();
                }
                return url;
            }
            finally
            {
                _sempahore.Release();
            }
        }
    }
}