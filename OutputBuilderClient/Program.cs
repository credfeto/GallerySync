using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Images;
using Newtonsoft.Json;
using ObjectModel;
using StorageHelpers;

namespace OutputBuilderClient
{
    internal class Program
    {
        private static readonly SemaphoreSlim _sempahore = new SemaphoreSlim(initialCount: 1);

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
            GC.GetTotalMemory(forceFullCollection: true);
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
            Console.WriteLine(value: "OutputBuilderClient");

            AlterPriority();

            return AsyncMain(args)
                .GetAwaiter()
                .GetResult();
        }

        private static void AlterPriority()
        {
            // TODO: Move to a common Library
            try
            {
                System.Diagnostics.Process.GetCurrentProcess()
                    .PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception)
            {
            }
        }

        private static async Task<int> AsyncMain(string[] args)
        {
            if (args.Length == 1)
            {
                ShortUrls.Load();

                try
                {
                    await StandaloneMetadata.ReadMetadata(args[0]);

                    return 0;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(format: "Error: {0}", exception.Message);
                    Console.WriteLine(format: "Stack Trace: {0}", exception.StackTrace);

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
                Console.WriteLine(format: "Error: {0}", exception.Message);
                Console.WriteLine(format: "Stack Trace: {0}", exception.StackTrace);

                retval = 1;
            }

            await DumpBrokenImages();

            return retval;
        }

        private static Task DumpBrokenImages()
        {
            string[] images = BrokenImages.AllBrokenImages();

            File.WriteAllLines(Settings.Default.BrokenImagesFile, images, Encoding.UTF8);

            return ConsoleOutput.Line(formatString: "Broken Images: {0}", images.Length);
        }

        private static async Task<HashSet<string>> Process(Photo[] source, Photo[] target)
        {
            ConcurrentDictionary<string, bool> items = new ConcurrentDictionary<string, bool>();

            await Task.WhenAll(source.Select(selector: sourcePhoto => ProcessSinglePhoto(target, sourcePhoto, items))
                                   .ToArray());

            return new HashSet<string>(items.Keys);
        }

        private static async Task ProcessGallery()
        {
            Task<Photo[]> sourceTask = PhotoMetadataRepository.LoadEmptyRepository(Settings.Default.RootFolder);
            Task<Photo[]> targetTask = PhotoMetadataRepository.LoadRepository(Settings.Default.DatabaseOutputFolder);

            await Task.WhenAll(sourceTask, targetTask);

            Photo[] source = sourceTask.Result;
            Photo[] target = targetTask.Result;

            HashSet<string> liveItems = await Process(source, target);

            await KillDeadItems(liveItems);
        }

        private static async Task ProcessOneFile(Photo sourcePhoto, Photo targetPhoto, bool rebuild, bool rebuildMetadata, string url, string shortUrl)
        {
            await ConsoleOutput.Line(rebuild ? "Rebuild: {0}" : "Build: {0}", sourcePhoto.UrlSafePath);

            await targetPhoto.UpdateFileHashes(sourcePhoto);

            bool buildMetadata = targetPhoto == null || rebuild || rebuildMetadata || targetPhoto != null && targetPhoto.Metadata == null;

            if (buildMetadata)
            {
                sourcePhoto.Metadata = MetadataExtraction.ExtractMetadata(sourcePhoto);
            }
            else
            {
                sourcePhoto.Metadata = targetPhoto.Metadata;
            }

            bool buildImages = targetPhoto == null || rebuild || targetPhoto != null && !targetPhoto.ImageSizes.HasAny();

            List<string> filesCreated = new List<string>();

            if (buildImages)
            {
                DateTime creationDate = MetadataHelpers.ExtractCreationDate(sourcePhoto.Metadata);
                sourcePhoto.ImageSizes = await ImageExtraction.BuildImages(sourcePhoto, filesCreated, creationDate, url, shortUrl);
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
                {
                    AddUploadFiles(filesCreated);
                }

                await PhotoMetadataRepository.Store(targetPhoto);
            }
            else
            {
                AddUploadFiles(filesCreated);

                await PhotoMetadataRepository.Store(sourcePhoto);
            }
        }

        private static async Task ProcessSinglePhoto(Photo[] target, Photo sourcePhoto, ConcurrentDictionary<string, bool> items)
        {
            ForceGarbageCollection();

            try
            {
                Photo targetPhoto = target.FirstOrDefault(predicate: item => item.PathHash == sourcePhoto.PathHash);
                bool build = targetPhoto == null;
                bool rebuild = targetPhoto != null && await RebuildDetection.NeedsFullResizedImageRebuild(sourcePhoto, targetPhoto);
                bool rebuildMetadata = targetPhoto != null && await RebuildDetection.MetadataVersionOutOfDate(targetPhoto);

                string url = "https://www.markridgwell.co.uk/albums/" + sourcePhoto.UrlSafePath;
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
                            await ConsoleOutput.Line(formatString: " +++ Force rebuild: missing shortcut URL.  New short url: {0}", shortUrl);
                        }
                    }
                }
                else
                {
                    if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                    {
                        await ConsoleOutput.Line(formatString: "* Reusing existing short url: {0}", shortUrl);
                    }
                }

                if (!string.IsNullOrWhiteSpace(shortUrl) && !StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                {
                    sourcePhoto.ShortUrl = shortUrl;
                }
                else
                {
                    shortUrl = Constants.DefaultShortUrl;
                }

                if (build || rebuild || rebuildMetadata)
                {
                    await ProcessOneFile(sourcePhoto, targetPhoto, rebuild, rebuildMetadata, url, shortUrl);
                }
                else
                {
                    await ConsoleOutput.Line(formatString: "Unchanged: {0}", targetPhoto.UrlSafePath);
                }

                items.TryAdd(sourcePhoto.PathHash, value: true);
            }
            catch (AbortProcessingException exception)
            {
                BrokenImages.LogBrokenImage(sourcePhoto.UrlSafePath, exception);

                throw;
            }
            catch (StackOverflowException exception)
            {
                BrokenImages.LogBrokenImage(sourcePhoto.UrlSafePath, exception);

                throw;
            }
            catch (Exception exception)
            {
                BrokenImages.LogBrokenImage(sourcePhoto.UrlSafePath, exception);
            }
        }

        private static async Task<string> TryGenerateShortUrl(string url)
        {
            string shortUrl;

            if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
            {
                return shortUrl;
            }

            await _sempahore.WaitAsync();

            if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
            {
                return shortUrl;
            }

            try
            {
                string filename = Settings.Default.ShortNamesFile + ".tracking.json";

                List<ShortenerCount> tracking = new List<ShortenerCount>();

                if (File.Exists(filename))
                {
                    byte[] bytes = await FileHelpers.ReadAllBytes(filename);

                    ShortenerCount[] items = JsonConvert.DeserializeObject<ShortenerCount[]>(Encoding.UTF8.GetString(bytes));

                    tracking.AddRange(items);
                }

                const int maxImpressionsPerMonth = 100;

                DateTime now = DateTime.UtcNow;

                ShortenerCount counter = tracking.FirstOrDefault(predicate: item => item.Year == now.Year && item.Month == now.Month);

                if (counter == null)
                {
                    counter = new ShortenerCount();

                    long totalImpressionsEver = 0L;

                    foreach (ShortenerCount month in tracking)
                    {
                        totalImpressionsEver += month.Impressions;
                    }

                    counter.Year = now.Year;
                    counter.Month = now.Month;
                    counter.Impressions = 1;
                    counter.TotalImpressionsEver = totalImpressionsEver;

                    tracking.Add(counter);

                    await FileHelpers.WriteAllBytes(filename, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(tracking.ToArray())));
                }
                else
                {
                    if (counter.Impressions < maxImpressionsPerMonth)
                    {
                        Console.WriteLine(format: "Bitly Impressions for {0}", counter.Impressions);
                        Console.WriteLine(format: "Bitly Impressions total {0}", counter.TotalImpressionsEver);
                        ++counter.Impressions;
                        ++counter.TotalImpressionsEver;

                        await FileHelpers.WriteAllBytes(filename, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(tracking.ToArray())));
                    }
                }

                if (counter.Impressions < maxImpressionsPerMonth)
                {
                    Uri shortened = await BitlyUrlShortner.Shorten(new Uri(url));

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

//    <?xml version='1.0' encoding='utf-8'?>
//<SettingsFile xmlns="http://schemas.microsoft.com/VisualStudio/2004/01/settings" CurrentProfile="(Default)" GeneratedClassNamespace="OutputBuilderClient.Properties" GeneratedClassName="Settings">
//  <Profiles />
//  <Settings>
//    <Setting Name="DatabaseOutputFolder" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">I:\Database\Current</Value>
//    </Setting>
//    <Setting Name="RootFolder" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">I:\Photos\Sorted</Value>
//    </Setting>
//    <Setting Name="ImageMaximumDimensions" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">400,600,800,1024,1600</Value>
//    </Setting>
//    <Setting Name="ThumbnailSize" Type="System.Int32" Scope="Application">
//      <Value Profile="(Default)">150</Value>
//    </Setting>
//    <Setting Name="ImageMagickConvertExecutable" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">D:\Utils\imageprocessing\convert.exe</Value>
//    </Setting>
//    <Setting Name="DCRAWExecutable" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">D:\Utils\imageprocessing\dcraw.exe</Value>
//    </Setting>
//    <Setting Name="JpegOutputQuality" Type="System.Int32" Scope="Application">
//      <Value Profile="(Default)">70</Value>
//    </Setting>
//    <Setting Name="WatermarkImage" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">D:\Utils\Gallery\watermark.png</Value>
//    </Setting>
//    <Setting Name="DatabaseBackupFolder" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">I:\Database\Backup\Current</Value>
//    </Setting>
//    <Setting Name="BitlyApiKey" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">R_43a3ce10699b39c57da408f60cb794ce</Value>
//    </Setting>
//    <Setting Name="BitlyApiUser" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">credfeto</Value>
//    </Setting>
//    <Setting Name="ImagesOutputPath" Type="System.String" Scope="Application">
//      <Value Profile="(Default)">\\nas-01.yggdrasil.local\GalleryUpload</Value>
//    </Setting>
//    <Setting Name="ShortNamesFile" Type="System.String" Scope="Application">
//          <Value Profile="(Default)">C:\PhotoDb\ShortUrls.csv</Value>
//    </Setting>
//    <Setting Name="BrokenImagesFile" Type="System.String" Scope="Application">
//              <Value Profile="(Default)">C:\PhotoDb\BrokenImages.csv</Value>
//        </Setting>
//    <Setting Name="LatestDatabaseBackupFolder" Type="System.String" Scope="User">
//      <Value Profile="(Default)">I:\Database\Backup\Latest</Value>
//    </Setting>
//  </Settings>
//</SettingsFile>
}