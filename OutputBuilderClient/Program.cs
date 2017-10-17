namespace OutputBuilderClient
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    using Alphaleonis.Win32.Filesystem;

    using FileNaming;

    using OutputBuilderClient.Properties;


    using Twaddle.Directory.Scanner;
    using Twaddle.Gallery.ObjectModel;

    internal class Program
    {
        private static readonly object Lock = new object();

        private static readonly Dictionary<string, string> ShorternedUrls = new Dictionary<string, string>();

        private static readonly object ShortUrlLock = new object();

        public static bool MetadataVersionRequiresRebuild(Photo targetPhoto)
        {
            if (MetadataVersionHelpers.RequiresRebuild(targetPhoto.Version))
            {
                OutputText(
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

        private static void BoostPriority()
        {
            try
            {
                System.Diagnostics.Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            }
            catch (Exception)
            {
            }
        }

        private static DateTime ExtractCreationDate(List<PhotoMetadata> metadata)
        {
            PhotoMetadata dateTaken = metadata.FirstOrDefault(candidate => candidate.Name == MetadataNames.DateTaken);
            if (dateTaken == null)
            {
                return DateTime.MinValue;
            }

            DateTime value;
            if (DateTime.TryParse(dateTaken.Value, out value))
            {
                return value;
            }

            return DateTime.MinValue;
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

            foreach (ImageSize resize in photoToProcess.ImageSizes)
            {
                string resizedFileName = Alphaleonis.Win32.Filesystem.Path.Combine(
                    Settings.Default.ImagesOutputPath,
                    HashNaming.PathifyHash(photoToProcess.PathHash),
                    ImageExtraction.IndividualResizeFileName(photoToProcess, resize));
                if (!Alphaleonis.Win32.Filesystem.File.Exists(resizedFileName))
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
                    resizedFileName = Alphaleonis.Win32.Filesystem.Path.Combine(
                        Settings.Default.ImagesOutputPath,
                        HashNaming.PathifyHash(photoToProcess.PathHash),
                        ImageExtraction.IndividualResizeFileName(photoToProcess, resize, "png"));
                    if (!Alphaleonis.Win32.Filesystem.File.Exists(resizedFileName))
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

        private static bool HaveFilesChanged(Photo sourcePhoto, Photo targetPhoto)
        {
            if (sourcePhoto.Files.Count != targetPhoto.Files.Count)
            {
                OutputText(" +++ Metadata update: File count changed");
                return true;
            }

            foreach (ComponentFile componentFile in targetPhoto.Files)
            {
                ComponentFile found =
                    sourcePhoto.Files.FirstOrDefault(
                        candiate =>
                        StringComparer.InvariantCultureIgnoreCase.Equals(candiate.Extension, componentFile.Extension));

                if (found != null)
                {
                    if (componentFile.FileSize != found.FileSize)
                    {
                        OutputText(" +++ Metadata update: File size changed (File: " + found.Extension + ")");
                        return true;
                    }

                    if (componentFile.LastModified == found.LastModified)
                    {
                        // Assume if file modified date not changed then the file itself hasn't changed
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(found.Hash))
                    {
                        string filename = Alphaleonis.Win32.Filesystem.Path.Combine(
                            Settings.Default.RootFolder,
                            sourcePhoto.BasePath + componentFile.Extension);

                        found.Hash = Hasher.HashFile(filename);
                    }

                    if (componentFile.Hash != found.Hash)
                    {
                        OutputText(" +++ Metadata update: File hash changed (File: " + found.Extension + ")");
                        return true;
                    }
                }
                else
                {
                    OutputText(" +++ Metadata update: File missing (File: " + componentFile.Extension + ")");
                    return true;
                }
            }

            return false;
        }

        private static void KillDeadItems( HashSet<string> liveItems)
        {
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

        private static void LoadShortUrls()
        {
            var logPath = Settings.Default.ShortNamesFile;

            if (Alphaleonis.Win32.Filesystem.File.Exists(logPath))
            {
                Console.WriteLine("Loading Existing Short Urls:");
                var lines = Alphaleonis.Win32.Filesystem.File.ReadAllLines(logPath);

                foreach (var line in lines)
                {
                    if (!line.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase)
                        && !line.StartsWith(@"https://", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var process = line.Trim().Split('\t');
                    if (process.Length != 2)
                    {
                        continue;
                    }

                    if (!ShorternedUrls.ContainsKey(process[0]))
                    {
                        ShorternedUrls.Add(process[0], process[1]);
                        Console.WriteLine("Loaded Short Url {0} for {1}", process[1], process[0]);
                    }
                }

                Console.WriteLine("Total Known Short Urls: {0}", ShorternedUrls.Count);
                Console.WriteLine();
            }
        }

        private static void LogShortUrl(string url, string shortUrl)
        {
            if (ShorternedUrls.ContainsKey(url))
            {
                return;
            }

            lock (ShortUrlLock)
            {
                ShorternedUrls.Add(url, shortUrl);
            }

            var logPath = Alphaleonis.Win32.Filesystem.Path.Combine(Settings.Default.ImagesOutputPath, "ShortUrls.csv");

            var text = new[] { string.Format("{0}\t{1}", url, shortUrl) };

            Alphaleonis.Win32.Filesystem.File.AppendAllLines(logPath, text);
        }

        private static int Main(string[] args)
        {
            Console.WriteLine("OutputBuilderClient");

            BoostPriority();

            if (args.Length == 1)
            {
                LoadShortUrls();

                try
                {
                    ReadMetadata(args[0]);

                    return 0;
                }
                catch (Exception exception)
                {
                    OutputText("Error: {0}", exception.Message);
                    OutputText("Stack Trace: {0}", exception.StackTrace);
                    return 1;
                }
            }

            try
            {
                LoadShortUrls();

                ProcessGallery();

                return 0;
            }
            catch (Exception exception)
            {
                OutputText("Error: {0}", exception.Message);
                OutputText("Stack Trace: {0}", exception.StackTrace);
                return 1;
            }
        }

        private static bool MetadataVersionOutOfDate(Photo targetPhoto)
        {
            if (MetadataVersionHelpers.IsOutOfDate(targetPhoto.Version))
            {
                OutputText(
                    " +++ Metadata update: Metadata version out of date. (Current: " + targetPhoto.Version
                    + " Expected: " + Constants.CurrentMetadataVersion + ")");
                return true;
            }

            return false;
        }

        private static bool NeedsFullResizedImageRebuild(Photo sourcePhoto, Photo targetPhoto)
        {
            return MetadataVersionRequiresRebuild(targetPhoto) || HaveFilesChanged(sourcePhoto, targetPhoto)
                   || HasMissingResizes(targetPhoto);
        }

        private static void OutputText(string formatString, params object[] parameters)
        {
            string text = string.Format(formatString, parameters);

            lock (Lock)
            {
                Console.WriteLine(text);
            }
        }

//        private static HashSet<string> Process(
//            EmbeddableDocumentStore documentStoreInput,
//            EmbeddableDocumentStore documentStoreOutput)
//        {
//            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
//            {
//                var items = new HashSet<string>();
//
//                var allPhotos = inputSession.GetAll<Photo>().ToArray();
//                var partitioner = Partitioner.Create(allPhotos, true);
//
//                var q = partitioner.AsParallel();
//
//                q.ForEach(
//                    sourcePhoto =>
//                        {
//                            ProcessSinglePhoto(documentStoreOutput, sourcePhoto, items);
//                        });
//
//                return items;
//            }
//        }

        private static void ProcessGallery()
        {
            List<Photo> source = LoadRepository(Settings.Default.DatabaseInputFolder);
            List<Photo> output = LoadRepository(Settings.Default.DatabaseOutputFolder);
            
//            var documentStoreInput = new EmbeddableDocumentStore { RunInMemory = true };
//            documentStoreInput.Initialize();
//            if (!documentStoreInput.Restore(Settings.Default.LatestDatabaseBackupFolder))
//            {
//                return;
//            }
//
//            string dbOutputFolder = Settings.Default.DatabaseOutputFolder;
//            bool restore = !Directory.Exists(dbOutputFolder) && Directory.Exists(Settings.Default.DatabaseBackupFolder);
//            if (!Directory.Exists(dbOutputFolder))
//            {
//                Directory.CreateDirectory(dbOutputFolder);
//            }
//
//            var documentStoreOutput = new EmbeddableDocumentStore
//                                          {
//                                              DataDirectory = dbOutputFolder,
//                                              RunInMemory = false
//                                          };
//
//            documentStoreOutput.Initialize();
//
//            if (restore)
//            {
//                documentStoreOutput.Restore(Settings.Default.DatabaseBackupFolder);
//            }
//
//            HashSet<string> liveItems = Process(documentStoreInput, documentStoreOutput);
//
//            KillDeadItems(documentStoreOutput, liveItems);
//
//            documentStoreOutput.Backup(Settings.Default.DatabaseBackupFolder);
        }

        private static List<Photo> LoadRepository(string baseFolder)
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
                long filesFound = DirectoryScanner.ScanFolder(baseFolder, emitter, scores.ToList(), sidecarFiles);

                Console.WriteLine("{0} : Files Found: {1}", baseFolder, filesFound);
            }
            
            return emitter.Photos;
        }

        private static void ProcessOneFile(
            //IDocumentSession outputSession,
            Photo sourcePhoto,
            Photo targetPhoto,
            bool rebuild,
            bool rebuildMetadata,
            string url,
            string shortUrl)
        {
            OutputText(rebuild ? "Rebuild: {0}" : "Build: {0}", sourcePhoto.UrlSafePath);

            UpdateFileHashes(targetPhoto, sourcePhoto);

            bool buildMetadata = targetPhoto == null || rebuild || rebuildMetadata
                                 || (targetPhoto != null && targetPhoto.Metadata == null);

            if (buildMetadata)
            {
                sourcePhoto.Metadata = MetadataExtraction.ExtractMetadata(sourcePhoto);
            }
            else
            {
                sourcePhoto.Metadata = targetPhoto.Metadata;
            }

            bool buildImages = targetPhoto == null || rebuild
                               || (targetPhoto != null && !targetPhoto.ImageSizes.HasAny());

            var filesCreated = new List<string>();
            if (buildImages)
            {
                DateTime creationDate = ExtractCreationDate(sourcePhoto.Metadata);
                sourcePhoto.ImageSizes = ImageExtraction.BuildImages(
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
                {
                    AddUploadFiles(filesCreated);
                }

                //TODO:
                //outputSession.Store(targetPhoto, targetPhoto.PathHash);
            }
            else
            {
                AddUploadFiles(filesCreated);
                // TODO
                //outputSession.Store(sourcePhoto, sourcePhoto.PathHash);
            }

            // TODO
            //outputSession.SaveChanges();
        }

        private static void ProcessSinglePhoto(
            //EmbeddableDocumentStore documentStoreOutput,
            Photo sourcePhoto,
            HashSet<string> items)
        {
            ForceGarbageCollection();

            try
            {
                //using (IDocumentSession outputSession = documentStoreOutput.OpenSession())
                {
                    // TODO:
                    Photo targetPhoto = null; //outputSession.Load<Photo>(sourcePhoto.PathHash);
                    bool build = targetPhoto == null;
                    bool rebuild = targetPhoto != null && NeedsFullResizedImageRebuild(sourcePhoto, targetPhoto);
                    bool rebuildMetadata = targetPhoto != null && MetadataVersionOutOfDate(targetPhoto);

                    string url = "https://www.markridgwell.co.uk/albums/" + sourcePhoto.UrlSafePath;
                    string shortUrl;

                    if (targetPhoto != null)
                    {
                        shortUrl = targetPhoto.ShortUrl;

                        if (ShouldGenerateShortUrl(sourcePhoto, shortUrl, url))
                        {
                            shortUrl = TryGenerateShortUrl(url);

                            if (!StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                            {
                                LogShortUrl(url, shortUrl);

                                rebuild = true;
                                Console.WriteLine(
                                    " +++ Force rebuild: missing shortcut URL.  New short url: {0}",
                                    shortUrl);
                            }
                        }
                    }
                    else
                    {
                        if (ShorternedUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                        {
                            Console.WriteLine("* Reusing existing short url: {0}", shortUrl);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(shortUrl)
                        && !StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                    {
                        sourcePhoto.ShortUrl = shortUrl;
                    }
                    else
                    {
                        shortUrl = Constants.DefaultShortUrl;
                    }

                    if (build || rebuild || rebuildMetadata)
                    {
                        ProcessOneFile(sourcePhoto, targetPhoto, rebuild, rebuildMetadata, url, shortUrl);
                    }
                    else
                    {
                        OutputText("Unchanged: {0}", targetPhoto.UrlSafePath);
                    }
                }

                lock (Lock)
                {
                    items.Add(sourcePhoto.PathHash);
                }
            }
            catch (AbortProcessingException exception)
            {
                OutputText(
                    "ERROR: Aborting at image {0} due to exception {1}",
                    sourcePhoto.UrlSafePath,
                    exception.Message);
                OutputText("Stack Trace: {0}", exception.StackTrace);
                throw;
            }
            catch (Exception exception)
            {
                OutputText("ERROR: Skipping image {0} due to exception {1}", sourcePhoto.UrlSafePath, exception.Message);
                OutputText("Stack Trace: {0}", exception.StackTrace);
            }
        }

        private static void ReadMetadata(string filename)
        {
            string folder = Alphaleonis.Win32.Filesystem.Path.GetDirectoryName(filename);
            string file = Alphaleonis.Win32.Filesystem.Path.GetFileName(filename);
            string extension = Alphaleonis.Win32.Filesystem.Path.GetExtension(filename);

            var fileGroup = new List<string>();
            if (Alphaleonis.Win32.Filesystem.File.Exists(filename.Replace(extension, ".xmp")))
            {
                fileGroup.Add(file.Replace(extension, ".xmp"));
            }

            var entry = new FileEntry
                            {
                                Folder = folder,
                                RelativeFolder = folder.Substring(Settings.Default.RootFolder.Length + 1),
                                LocalFileName = file,
                                AlternateFileNames = fileGroup
                            };

            string basePath = Alphaleonis.Win32.Filesystem.Path.Combine(
                entry.RelativeFolder,
                Alphaleonis.Win32.Filesystem.Path.GetFileNameWithoutExtension(entry.LocalFileName));

            string urlSafePath = UrlNaming.BuildUrlSafePath(basePath);

            var photo = new Photo
                            {
                                BasePath = basePath,
                                UrlSafePath = urlSafePath,
                                PathHash = Hasher.HashBytes(Encoding.UTF8.GetBytes(urlSafePath)),
                                ImageExtension = Alphaleonis.Win32.Filesystem.Path.GetExtension(entry.LocalFileName),
                                Files =
                                    fileGroup.Select(
                                        x =>
                                        new ComponentFile
                                            {
                                                Extension =
                                                    Alphaleonis.Win32.Filesystem.Path.GetExtension(x)
                                                    .TrimStart('.'),
                                                Hash = string.Empty,
                                                LastModified = new DateTime(2014, 1, 1),
                                                FileSize = 1000
                                            }).ToList()
                            };

            List<PhotoMetadata> metadata = MetadataExtraction.ExtractMetadata(photo);
            foreach (PhotoMetadata item in metadata)
            {
                Console.WriteLine("{0} = {1}", item.Name, item.Value);
            }
        }

        private static bool ShouldGenerateShortUrl(Photo sourcePhoto, string shortUrl, string url)
        {
            // ONly want to generate a short URL, IF the photo has already been uploaded AND is public
            if (sourcePhoto.UrlSafePath.StartsWith("private/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(shortUrl)
                   || StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url)
                   || StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, Constants.DefaultShortUrl);
        }

        private static string TryGenerateShortUrl(string url)
        {
            string shortUrl;
            if (ShorternedUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
            {
                return shortUrl;
            }

            //using (IDocumentSession shortenerSession = documentStoreOutput.OpenSession())
            {
                //const int maxImpressionsPerMonth = 100;

                //const string tag = @"BitlyShortenerStats";
                //DateTime now = DateTime.UtcNow;
                
                // TODO:
//                var counter = shortenerSession.Load<ShortenerCount>(tag);
//                if (counter == null)
//                {
//                    counter = new ShortenerCount();
//
//                    counter.Year = now.Year;
//                    counter.Month = now.Month;
//                    counter.Impressions = 1;
//                    counter.TotalImpressionsEver = 1;
//
//                    shortenerSession.Store(counter, tag);
//                    shortenerSession.SaveChanges();
//                }
//                else
//                {
//                    if (counter.Year != now.Year || counter.Month != now.Month)
//                    {
//                        counter.Impressions = 0;
//
//                        if (counter.Year == 2015 && counter.Month == 4)
//                        {
//                            counter.Impressions = maxImpressionsPerMonth * 10;
//                        }
//                    }
//
//                    if (counter.Impressions < maxImpressionsPerMonth)
//                    {
//                        Console.WriteLine("Bitly Impressions for {0}", counter.Impressions);
//                        Console.WriteLine("Bitly Impressions total {0}", counter.TotalImpressionsEver);
//                        ++counter.Impressions;
//                        ++counter.TotalImpressionsEver;
//
//                        shortenerSession.Store(counter, tag);
//                        shortenerSession.SaveChanges();
//                    }
//                }
//
//                if (counter.Impressions < maxImpressionsPerMonth)
//                {
//                    return BitlyUrlShortner.Shorten(new Uri(url)).ToString();
//                }
//                else
//                {
//                    return url;
//                }
                return url;
            }
        }

        private static void UpdateFileHashes(Photo targetPhoto, Photo sourcePhoto)
        {
            if (targetPhoto != null)
            {
                foreach (ComponentFile sourceFile in
                    sourcePhoto.Files.Where(s => string.IsNullOrWhiteSpace(s.Hash)))
                {
                    ComponentFile targetFile =
                        targetPhoto.Files.FirstOrDefault(
                            s => s.Extension == sourceFile.Extension && !string.IsNullOrWhiteSpace(s.Hash));
                    if (targetFile != null)
                    {
                        sourceFile.Hash = targetFile.Hash;
                    }
                }
            }

            foreach (ComponentFile file in
                sourcePhoto.Files.Where(s => string.IsNullOrWhiteSpace(s.Hash)))
            {
                string filename = Alphaleonis.Win32.Filesystem.Path.Combine(
                    Settings.Default.RootFolder,
                    sourcePhoto.BasePath + file.Extension);

                file.Hash = Hasher.HashFile(filename);
            }
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