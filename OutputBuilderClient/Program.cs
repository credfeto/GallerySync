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
        private static readonly ConcurrentDictionary<string, string> _brokenImages =
            new ConcurrentDictionary<string, string>();

        private static readonly SemaphoreSlim _sempahore = new SemaphoreSlim(1);
        private static readonly SemaphoreSlim _consoleSempahore = new SemaphoreSlim(1);

        public static async Task<bool> MetadataVersionRequiresRebuild(Photo targetPhoto)
        {
            if (MetadataVersionHelpers.RequiresRebuild(targetPhoto.Version))
            {
                await OutputText(
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

        private static bool HasMissingResizes(Photo photoToProcess)
        {
            if (photoToProcess.ImageSizes == null)
            {
                Console.WriteLine(" +++ Force rebuild: No image sizes at all!");
                return true;
            }

            foreach (var resize in photoToProcess.ImageSizes)
            {
                var resizedFileName = Path.Combine(
                    Settings.Default.ImagesOutputPath,
                    HashNaming.PathifyHash(photoToProcess.PathHash),
                    ImageExtraction.IndividualResizeFileName(photoToProcess, resize));
                if (!File.Exists(resizedFileName))
                {
                    Console.WriteLine(
                        " +++ Force rebuild: Missing image for size {0}x{1} (jpg)",
                        resize.Width,
                        resize.Height);
                    return true;
                }

                // Moving this to a separate program
                //try
                //{
                //    byte[] bytes = Alphaleonis.Win32.Filesystem.File.ReadAllBytes(resizedFileName);

                //    if (!ImageHelpers.IsValidJpegImage(bytes, "Existing: " + resizedFileName))
                //    {
                //        Console.WriteLine(" +++ Force rebuild: image for size {0}x{1} is not a valid jpg", resize.Width,
                //                          resize.Height);
                //        return true;
                //    }
                //}
                //catch( Exception exception)
                //{
                //    Console.WriteLine(" +++ Force rebuild: image for size {0}x{1} is missing/corrupt - Exception: {2}", resize.Width,
                //                      resize.Height, exception.Message);
                //    return true;
                //}
                if (resize.Width == Settings.Default.ThumbnailSize)
                {
                    resizedFileName = Path.Combine(
                        Settings.Default.ImagesOutputPath,
                        HashNaming.PathifyHash(photoToProcess.PathHash),
                        ImageExtraction.IndividualResizeFileName(photoToProcess, resize, "png"));
                    if (!File.Exists(resizedFileName))
                    {
                        Console.WriteLine(
                            " +++ Force rebuild: Missing image for size {0}x{1} (png)",
                            resize.Width,
                            resize.Height);
                        return true;
                    }
                }
            }

            return false;
        }

        private static async Task<bool> HaveFilesChanged(Photo sourcePhoto, Photo targetPhoto)
        {
            if (sourcePhoto.Files.Count != targetPhoto.Files.Count)
            {
                await OutputText(" +++ Metadata update: File count changed");
                return true;
            }

            foreach (var componentFile in targetPhoto.Files)
            {
                var found =
                    sourcePhoto.Files.FirstOrDefault(
                        candiate =>
                            StringComparer.InvariantCultureIgnoreCase.Equals(candiate.Extension,
                                componentFile.Extension));

                if (found != null)
                {
                    if (componentFile.FileSize != found.FileSize)
                    {
                        await OutputText(" +++ Metadata update: File size changed (File: " + found.Extension + ")");
                        return true;
                    }

                    if (componentFile.LastModified == found.LastModified)
                        continue;

                    if (string.IsNullOrWhiteSpace(found.Hash))
                    {
                        var filename = Path.Combine(
                            Settings.Default.RootFolder,
                            sourcePhoto.BasePath + componentFile.Extension);

                        found.Hash = await Hasher.HashFile(filename);
                    }

                    if (componentFile.Hash != found.Hash)
                    {
                        await OutputText(" +++ Metadata update: File hash changed (File: " + found.Extension + ")");
                        return true;
                    }
                }
                else
                {
                    await OutputText(" +++ Metadata update: File missing (File: " + componentFile.Extension + ")");
                    return true;
                }
            }

            return false;
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
                    ReadMetadata(args[0]);

                    return 0;
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Error: {0}", exception.Message);
                    Console.WriteLine("Stack Trace: {0}", exception.StackTrace);
                    return 1;
                }
            }

            try
            {
                ShortUrls.Load();

                await ProcessGallery();

                DumpBrokenImages();
                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: {0}", exception.Message);
                Console.WriteLine("Stack Trace: {0}", exception.StackTrace);

                DumpBrokenImages();

                return 1;
            }
        }

        private static void DumpBrokenImages()
        {
            var images = _brokenImages.OrderBy(item => item.Key)
                .Select(item => string.Concat(item.Key, "\t", item.Value)).ToArray();

            File.WriteAllLines(Settings.Default.BrokenImagesFile, images, Encoding.UTF8);

            Console.WriteLine("Broken Images: {0}", images.Length);
        }

        private static async Task<bool> MetadataVersionOutOfDate(Photo targetPhoto)
        {
            if (MetadataVersionHelpers.IsOutOfDate(targetPhoto.Version))
            {
                await OutputText(
                    " +++ Metadata update: Metadata version out of date. (Current: " + targetPhoto.Version
                    + " Expected: " + Constants.CurrentMetadataVersion + ")");
                return true;
            }

            return false;
        }

        private static async Task<bool> NeedsFullResizedImageRebuild(Photo sourcePhoto, Photo targetPhoto)
        {
            return await MetadataVersionRequiresRebuild(targetPhoto) || await HaveFilesChanged(sourcePhoto, targetPhoto)
                   || HasMissingResizes(targetPhoto);
        }

        private static async Task OutputText(string formatString, params object[] parameters)
        {
            var text = string.Format(formatString, parameters);

            await _consoleSempahore.WaitAsync();
            try
            {
                Console.WriteLine(text);
            }
            finally
            {
                _consoleSempahore.Release();
            }
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
            await OutputText(rebuild ? "Rebuild: {0}" : "Build: {0}", sourcePhoto.UrlSafePath);

            await UpdateFileHashes(targetPhoto, sourcePhoto);

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
                UpdateTargetWithSourceProperties(targetPhoto, sourcePhoto);
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
                var rebuild = targetPhoto != null && await NeedsFullResizedImageRebuild(sourcePhoto, targetPhoto);
                var rebuildMetadata = targetPhoto != null && await MetadataVersionOutOfDate(targetPhoto);

                var url = "https://www.markridgwell.co.uk/albums/" + sourcePhoto.UrlSafePath;
                string shortUrl;

                if (targetPhoto != null)
                {
                    shortUrl = targetPhoto.ShortUrl;

                    if (ShouldGenerateShortUrl(sourcePhoto, shortUrl, url))
                    {
                        shortUrl = await TryGenerateShortUrl(url);

                        if (!StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                        {
                            ShortUrls.LogShortUrl(url, shortUrl);

                            rebuild = true;
                            Console.WriteLine(
                                " +++ Force rebuild: missing shortcut URL.  New short url: {0}",
                                shortUrl);
                        }
                    }
                }
                else
                {
                    if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                        Console.WriteLine("* Reusing existing short url: {0}", shortUrl);
                }

                if (!string.IsNullOrWhiteSpace(shortUrl)
                    && !StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                    sourcePhoto.ShortUrl = shortUrl;
                else
                    shortUrl = Constants.DefaultShortUrl;

                if (build || rebuild || rebuildMetadata)
                    await ProcessOneFile(sourcePhoto, targetPhoto, rebuild, rebuildMetadata, url, shortUrl);
                else
                    await OutputText("Unchanged: {0}", targetPhoto.UrlSafePath);

                items.TryAdd(sourcePhoto.PathHash, true);
            }
            catch (AbortProcessingException exception)
            {
                LogBrokenImage(sourcePhoto.UrlSafePath, exception.Message);
                throw;
            }
            catch (Exception exception)
            {
                LogBrokenImage(sourcePhoto.UrlSafePath, exception.Message);
            }
        }

        private static void LogBrokenImage(string path, string message)
        {
            _brokenImages.TryAdd(path, message);
        }

        private static void ReadMetadata(string filename)
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
                Console.WriteLine("{0} = {1}", item.Name, item.Value);
        }

        private static bool ShouldGenerateShortUrl(Photo sourcePhoto, string shortUrl, string url)
        {
            // ONly want to generate a short URL, IF the photo has already been uploaded AND is public
            if (sourcePhoto.UrlSafePath.StartsWith("private/", StringComparison.OrdinalIgnoreCase))
                return false;

            return string.IsNullOrWhiteSpace(shortUrl)
                   || StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url)
                   || StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, Constants.DefaultShortUrl);
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

        private static Task UpdateFileHashes(Photo targetPhoto, Photo sourcePhoto)
        {
            if (targetPhoto != null)
                foreach (var sourceFile in
                    sourcePhoto.Files.Where(s => string.IsNullOrWhiteSpace(s.Hash)))
                {
                    var targetFile =
                        targetPhoto.Files.FirstOrDefault(
                            s => s.Extension == sourceFile.Extension && !string.IsNullOrWhiteSpace(s.Hash));
                    if (targetFile != null)
                        sourceFile.Hash = targetFile.Hash;
                }

            return Task.WhenAll(
                sourcePhoto.Files.Where(s => string.IsNullOrWhiteSpace(s.Hash))
                    .Select(file => SetFileHash(sourcePhoto, file)));
        }

        private static async Task SetFileHash(Photo sourcePhoto, ComponentFile file)
        {
            var filename = Path.Combine(
                Settings.Default.RootFolder,
                sourcePhoto.BasePath + file.Extension);

            file.Hash = await Hasher.HashFile(filename);
        }

        private static void UpdateTargetWithSourceProperties(Photo targetPhoto, Photo sourcePhoto)
        {
            targetPhoto.Version = sourcePhoto.Version;
            targetPhoto.UrlSafePath = sourcePhoto.UrlSafePath;
            targetPhoto.BasePath = sourcePhoto.BasePath;
            targetPhoto.PathHash = sourcePhoto.PathHash;
            targetPhoto.ImageExtension = sourcePhoto.ImageExtension;
            targetPhoto.Files = sourcePhoto.Files;
            targetPhoto.Metadata = sourcePhoto.Metadata;
            targetPhoto.ImageSizes = sourcePhoto.ImageSizes;
            targetPhoto.ShortUrl = sourcePhoto.ShortUrl;
        }
    }
}