using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageLoader.Core;
using ImageLoader.Interfaces;
using ImageLoader.Photoshop;
using ImageLoader.Raw;
using ImageLoader.Standard;
using Images;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using ObjectModel;
using StorageHelpers;

namespace OutputBuilderClient
{
    internal static class Program
    {
        private static readonly SemaphoreSlim Sempahore = new SemaphoreSlim(initialCount: 1);

        private static void ForceGarbageCollection()
        {
            GC.GetTotalMemory(forceFullCollection: true);
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
            catch
            {
                // Don't care'
            }
        }

        private static async Task<int> AsyncMain(string[] args)
        {
            IConfigurationRoot config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                                                                  .AddJsonFile(path: "appsettings.json")
                                                                  .AddCommandLine(args,
                                                                                  new Dictionary<string, string>
                                                                                  {
                                                                                      {@"-source", @"Source:RootFolder"},
                                                                                      {@"-output", @"Database:OutputFolder"},
                                                                                      {@"-imageoutput", @"Output:ImagesOutputPath"},
                                                                                      {@"-brokenImages", @"Output:BrokenImagesFile"},
                                                                                      {@"-shortUrls", @"Output:ShortUrls"},
                                                                                      {@"-watermark", @"Images:Watermark"},
                                                                                      {@"-thumbnailSize", @"Output:ThumbnailSize"},
                                                                                      {@"-quality", @"Output:JpegOutputQuality"},
                                                                                      {@"-resizes", @"Output:ImageMaximumDimensions"}
                                                                                  })
                                                                  .Build();

            Settings.RootFolder = config.GetValue<string>(key: @"Source:RootFolder");
            Settings.DatabaseOutputFolder = config.GetValue<string>(key: @"Database:OutputFolder");
            Settings.ShortNamesFile = config.GetValue<string>(key: @"Output:ShortUrls");
            Settings.BrokenImagesFile = config.GetValue<string>(key: @"Output:BrokenImagesFile");
            Settings.BitlyApiUser = config.GetValue<string>(key: @"UrlShortener:BitlyApiUser");
            Settings.BitlyApiKey = config.GetValue<string>(key: @"UrlShortener:BitlyApiKey");

            ISettings imageSettings = new ImageSettings(thumbnailSize: config.GetValue(key: @"Output:ThumbnailSize", defaultValue: 150),
                                                        defaultShortUrl: @"https://www.markridgwell.co.uk",
                                                        imageMaximumDimensions: config.GetValue(key: @"Output:ImageMaximumDimensions", defaultValue: @"400,600,800,1024,1600"),
                                                        rootFolder: Settings.RootFolder,
                                                        imagesOutputPath: config.GetValue<string>(key: @"Output:ImagesOutputPath"),
                                                        jpegOutputQuality: config.GetValue(key: @"Output:JpegOutputQuality", defaultValue: 100),
                                                        watermarkImage: config.GetValue<string>(key: @"Images:Watermark"));

            ServiceCollection serviceCollection = RegisterServices();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

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

                IImageLoader imageLoader = serviceProvider.GetService<IImageLoader>();

                await ProcessGallery(imageSettings, imageLoader);

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

        private static ServiceCollection RegisterServices()
        {
            ServiceCollection serviceCollection = new ServiceCollection();

            ImageLoaderCoreSetup.Configure(serviceCollection);
            ImageLoaderStandardSetup.Configure(serviceCollection);
            ImageLoaderRawSetup.Configure(serviceCollection);
            ImageLoaderPhotoshopSetup.Configure(serviceCollection);

            return serviceCollection;
        }

        private static Task DumpBrokenImages()
        {
            string[] images = BrokenImages.AllBrokenImages();

            File.WriteAllLines(Settings.BrokenImagesFile, images, Encoding.UTF8);

            return ConsoleOutput.Line(formatString: "Broken Images: {0}", images.Length);
        }

        private static async Task<HashSet<string>> Process(IImageLoader imageLoader, Photo[] source, Photo[] target, ISettings imageSettings)
        {
            ConcurrentDictionary<string, bool> items = new ConcurrentDictionary<string, bool>();

            await Task.WhenAll(source.Select(selector: sourcePhoto => ProcessSinglePhoto(imageLoader, target, sourcePhoto, items, imageSettings))
                                     .ToArray());

            return new HashSet<string>(items.Keys);
        }

        private static async Task ProcessGallery(ISettings imageSettings, IImageLoader imageLoader)
        {
            Task<Photo[]> sourceTask = PhotoMetadataRepository.LoadEmptyRepository(Settings.RootFolder);
            Task<Photo[]> targetTask = PhotoMetadataRepository.LoadRepository(Settings.DatabaseOutputFolder);

            await Task.WhenAll(sourceTask, targetTask);

            Photo[] source = sourceTask.Result;
            Photo[] target = targetTask.Result;

            await Process(imageLoader, source, target, imageSettings);
        }

        private static async Task ProcessOneFile(IImageLoader imageLoader,
                                                 Photo sourcePhoto,
                                                 Photo targetPhoto,
                                                 bool rebuild,
                                                 bool rebuildMetadata,
                                                 string url,
                                                 string shortUrl,
                                                 ISettings imageSettings)
        {
            await ConsoleOutput.Line(rebuild ? "Rebuild: {0}" : "Build: {0}", sourcePhoto.UrlSafePath);

            await targetPhoto.UpdateFileHashes(sourcePhoto);

            bool buildMetadata = targetPhoto == null || rebuild || rebuildMetadata || targetPhoto.Metadata == null;

            if (buildMetadata)
            {
                sourcePhoto.Metadata = MetadataExtraction.ExtractMetadata(sourcePhoto);
            }
            else
            {
                sourcePhoto.Metadata = targetPhoto.Metadata;
            }

            bool buildImages = targetPhoto == null || rebuild || !targetPhoto.ImageSizes.HasAny();

            List<string> filesCreated = new List<string>();

            if (buildImages)
            {
                DateTime creationDate = MetadataHelpers.ExtractCreationDate(sourcePhoto.Metadata);

                sourcePhoto.ImageSizes = await ImageExtraction.BuildImages(imageLoader, sourcePhoto, filesCreated, creationDate, url, shortUrl, imageSettings);
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
                    //?
                }

                await PhotoMetadataRepository.Store(targetPhoto);
            }
            else
            {
                await PhotoMetadataRepository.Store(sourcePhoto);
            }
        }

        private static async Task ProcessSinglePhoto(IImageLoader imageLoader, Photo[] target, Photo sourcePhoto, ConcurrentDictionary<string, bool> items, ISettings imageSettings)
        {
            ForceGarbageCollection();

            try
            {
                Photo targetPhoto = target.FirstOrDefault(predicate: item => item.PathHash == sourcePhoto.PathHash);
                bool build = targetPhoto == null;
                bool rebuild = targetPhoto != null && await RebuildDetection.NeedsFullResizedImageRebuild(sourcePhoto, targetPhoto, imageSettings);
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
                            ShortUrls.LogShortUrl(url, shortUrl, imageSettings);

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
                    await ProcessOneFile(imageLoader, sourcePhoto, targetPhoto, rebuild, rebuildMetadata, url, shortUrl, imageSettings);
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
            if (ShortUrls.TryGetValue(url, out string shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
            {
                return shortUrl;
            }

            await Sempahore.WaitAsync();

            if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
            {
                return shortUrl;
            }

            try
            {
                string filename = Settings.ShortNamesFile + ".tracking.json";

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
                Sempahore.Release();
            }
        }
    }
}