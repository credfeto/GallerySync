using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
using Microsoft.Extensions.Logging;
using ObjectModel;
using Serilog;
using StorageHelpers;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace OutputBuilderClient
{
    internal static class Program
    {
        private static readonly SemaphoreSlim Sempahore = new SemaphoreSlim(initialCount: 1);

        private static void ForceGarbageCollection()
        {
            GC.GetTotalMemory(forceFullCollection: true);
        }

        private static async Task<int> Main(string[] args)
        {
            Console.WriteLine(value: "OutputBuilderClient");

            AlterPriority();

            ServiceCollection serviceCollection = RegisterServices();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            loggerFactory.AddSerilog();

            ILogger logging = loggerFactory.CreateLogger(categoryName: "OutputBuilderClient");

            const string rootFolder = @"Source:RootFolder";

            const string databaseOutputFolder = @"Database:OutputFolder";
            const string outputShortUrls = @"Output:ShortUrls";
            const string outputImages = @"Output:ImagesOutputPath";
            const string outputBrokenImages = @"Output:BrokenImagesFile";
            const string watermark = @"Images:Watermark";

            const string outputJpegQuality = @"Output:JpegOutputQuality";
            const string outputMaximumDimensions = @"Output:ImageMaximumDimensions";
            const string outputThumbnailSize = @"Output:ThumbnailSize";

            IConfigurationRoot config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                                                                  .AddJsonFile(path: "appsettings.json", optional: true)
                                                                  .AddCommandLine(args,
                                                                                  new Dictionary<string, string>
                                                                                  {
                                                                                      {@"-source", rootFolder},
                                                                                      {@"-output", databaseOutputFolder},
                                                                                      {@"-imageoutput", outputImages},
                                                                                      {@"-brokenImages", outputBrokenImages},
                                                                                      {@"-shortUrls", outputShortUrls},
                                                                                      {@"-watermark", watermark},
                                                                                      {@"-thumbnailSize", outputThumbnailSize},
                                                                                      {@"-quality", outputJpegQuality},
                                                                                      {@"-resizes", outputMaximumDimensions}
                                                                                  })
                                                                  .Build();

            Settings.RootFolder = config.GetValue<string>(rootFolder);
            Settings.DatabaseOutputFolder = config.GetValue<string>(databaseOutputFolder);
            Settings.ShortNamesFile = config.GetValue<string>(outputShortUrls);
            Settings.BrokenImagesFile = config.GetValue<string>(outputBrokenImages);
            Settings.BitlyApiUser = config.GetValue<string>(key: @"UrlShortener:BitlyApiUser");
            Settings.BitlyApiKey = config.GetValue<string>(key: @"UrlShortener:BitlyApiKey");

            ISettings imageSettings = new ImageSettings(thumbnailSize: config.GetValue(outputThumbnailSize, defaultValue: 150),
                                                        shortUrlsPath: Settings.ShortNamesFile,
                                                        defaultShortUrl: @"https://www.markridgwell.co.uk",
                                                        imageMaximumDimensions: config.GetValue(outputMaximumDimensions, defaultValue: @"400,600,800,1024,1600"),
                                                        rootFolder: Settings.RootFolder,
                                                        imagesOutputPath: config.GetValue<string>(outputImages),
                                                        jpegOutputQuality: config.GetValue(outputJpegQuality, defaultValue: 100),
                                                        watermarkImage: config.GetValue<string>(watermark));

            Console.WriteLine($"Source: {Settings.RootFolder}");
            Console.WriteLine($"Output: {Settings.DatabaseOutputFolder}");
            Console.WriteLine($"Images: {imageSettings.RootFolder}");
            Console.WriteLine($"Thumb:  {imageSettings.ThumbnailSize}");

            foreach (int resize in imageSettings.ImageMaximumDimensions)
            {
                Console.WriteLine($"Resize: {resize}");
            }

            try
            {
                ShortUrls.Load();

                IImageLoader imageLoader = serviceProvider.GetService<IImageLoader>();

                Console.WriteLine($"Supported Extensions: {string.Join(separator: ", ", imageLoader.SupportedExtensions)}");

                await ProcessGalleryAsync(imageSettings, imageLoader, logging);

                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine(format: "Error: {0}", exception.Message);
                Console.WriteLine(format: "Stack Trace: {0}", exception.StackTrace);

                return 1;
            }
            finally
            {
                await DumpBrokenImagesAsync(logging);
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
                // Don't care'
            }
        }

        private static ServiceCollection RegisterServices()
        {
            ServiceCollection serviceCollection = new ServiceCollection();

            Log.Logger = new LoggerConfiguration().Enrich.FromLogContext()
                                                  .WriteTo.Console()
                                                  .CreateLogger();

            serviceCollection.AddOptions()
                             .AddLogging();

            ImageLoaderCoreSetup.Configure(serviceCollection);
            ImageLoaderStandardSetup.Configure(serviceCollection);
            ImageLoaderRawSetup.Configure(serviceCollection);
            ImageLoaderPhotoshopSetup.Configure(serviceCollection);

            return serviceCollection;
        }

        private static async Task DumpBrokenImagesAsync(ILogger logging)
        {
            string[] images = BrokenImages.AllBrokenImages();

            await File.WriteAllLinesAsync(Settings.BrokenImagesFile, images, Encoding.UTF8);

            logging.LogInformation($"Broken Images: {images.Length}");
        }

        private static async Task<HashSet<string>> ProcessAsync(IImageLoader imageLoader, Photo[] source, Photo[] target, ISettings imageSettings, ILogger logging)
        {
            ConcurrentDictionary<string, bool> items = new ConcurrentDictionary<string, bool>();

            await Task.WhenAll(source.Select(selector: sourcePhoto => ProcessSinglePhotoAsync(imageLoader, target, sourcePhoto, items, imageSettings, logging))
                                     .ToArray());

            return new HashSet<string>(items.Keys);
        }

        private static async Task ProcessGalleryAsync(ISettings imageSettings, IImageLoader imageLoader, ILogger logging)
        {
            Task<Photo[]> sourceTask = PhotoMetadataRepository.LoadEmptyRepositoryAsync(Settings.RootFolder, logging);
            Task<Photo[]> targetTask = PhotoMetadataRepository.LoadRepositoryAsync(Settings.DatabaseOutputFolder, logging);

            Photo[][] results = await Task.WhenAll(sourceTask, targetTask);

            Photo[] source = results[0];
            Photo[] target = results[1];

            await ProcessAsync(imageLoader, source, target, imageSettings, logging);
        }

        private static async Task ProcessOneFileAsync(IImageLoader imageLoader,
                                                      Photo sourcePhoto,
                                                      Photo targetPhoto,
                                                      bool rebuild,
                                                      bool rebuildMetadata,
                                                      string url,
                                                      string shortUrl,
                                                      ISettings imageSettings,
                                                      ILogger logging)
        {
            logging.LogInformation(rebuild ? $"Rebuild: {sourcePhoto.UrlSafePath}" : $"Build: {sourcePhoto.UrlSafePath}");

            await targetPhoto.UpdateFileHashesAsync(sourcePhoto);

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
                logging.LogInformation(message: "Build images:");
                DateTime creationDate = MetadataHelpers.ExtractCreationDate(sourcePhoto.Metadata);

                try
                {
                    sourcePhoto.ImageSizes = await ImageExtraction.BuildImagesAsync(imageLoader, sourcePhoto, filesCreated, creationDate, url, shortUrl, imageSettings, logging);
                }
                catch (Exception exception)
                {
                    logging.LogInformation($" Failed to load image: {sourcePhoto.UrlSafePath}: {exception.Message}");

                    throw;
                }
            }
            else
            {
                logging.LogInformation(message: "Not building images");
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

                await PhotoMetadataRepository.StoreAsync(targetPhoto);
            }
            else
            {
                await PhotoMetadataRepository.StoreAsync(sourcePhoto);
            }
        }

        private static async Task ProcessSinglePhotoAsync(IImageLoader imageLoader,
                                                          Photo[] target,
                                                          Photo sourcePhoto,
                                                          ConcurrentDictionary<string, bool> items,
                                                          ISettings imageSettings,
                                                          ILogger logging)
        {
            ForceGarbageCollection();

            try
            {
                Photo targetPhoto = target.FirstOrDefault(predicate: item => item.PathHash == sourcePhoto.PathHash);
                bool build = targetPhoto == null;
                bool rebuild = targetPhoto != null && await RebuildDetection.NeedsFullResizedImageRebuildAsync(sourcePhoto, targetPhoto, imageSettings, logging);
                bool rebuildMetadata = targetPhoto != null && RebuildDetection.MetadataVersionOutOfDate(targetPhoto, logging);

                string url = "https://www.markridgwell.co.uk/albums/" + sourcePhoto.UrlSafePath;
                string shortUrl;

                if (targetPhoto != null)
                {
                    shortUrl = targetPhoto.ShortUrl;

                    if (ShortUrls.ShouldGenerateShortUrl(sourcePhoto, shortUrl, url))
                    {
                        shortUrl = await TryGenerateShortUrlAsync(url);

                        if (!StringComparer.InvariantCultureIgnoreCase.Equals(shortUrl, url))
                        {
                            ShortUrls.LogShortUrl(url, shortUrl, imageSettings);

                            rebuild = true;
                            logging.LogInformation($" +++ Force rebuild: missing shortcut URL.  New short url: {shortUrl}");
                        }
                    }
                }
                else
                {
                    if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                    {
                        logging.LogInformation($"* Reusing existing short url: {shortUrl}");
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
                    await ProcessOneFileAsync(imageLoader, sourcePhoto, targetPhoto, rebuild, rebuildMetadata, url, shortUrl, imageSettings, logging);
                }
                else
                {
                    logging.LogInformation($"Unchanged: {targetPhoto.UrlSafePath}");
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

        private static async Task<string> TryGenerateShortUrlAsync(string url)
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
                    byte[] bytes = await FileHelpers.ReadAllBytesAsync(filename);

                    ShortenerCount[] items = JsonSerializer.Deserialize<ShortenerCount[]>(Encoding.UTF8.GetString(bytes));

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

                    await FileHelpers.WriteAllBytesAsync(filename, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tracking.ToArray())), commit: false);
                }
                else
                {
                    if (counter.Impressions < maxImpressionsPerMonth)
                    {
                        Console.WriteLine(format: "Bitly Impressions for {0}", counter.Impressions);
                        Console.WriteLine(format: "Bitly Impressions total {0}", counter.TotalImpressionsEver);
                        ++counter.Impressions;
                        ++counter.TotalImpressionsEver;

                        await FileHelpers.WriteAllBytesAsync(filename, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tracking.ToArray())), commit: false);
                    }
                }

                if (counter.Impressions < maxImpressionsPerMonth)
                {
                    Uri shortened = await BitlyUrlShortner.ShortenAsync(new Uri(url));

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