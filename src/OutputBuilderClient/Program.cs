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

        private static async Task<int> Main(string[] args)
        {
            Console.WriteLine(value: "OutputBuilderClient");

            AlterPriority();

            int ret;

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

            ServiceCollection serviceCollection = RegisterServices();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            if (args.Length == 1)
            {
                ShortUrls.Load();

                try
                {
                    await StandaloneMetadata.ReadMetadataAsync(args[0]);

                    ret = 0;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(format: "Error: {0}", exception.Message);
                    Console.WriteLine(format: "Stack Trace: {0}", exception.StackTrace);

                    ret = 1;
                }
            }
            else
            {
                int retval;

                try
                {
                    ShortUrls.Load();

                    IImageLoader imageLoader = serviceProvider.GetService<IImageLoader>();

                    Console.WriteLine($"Supported Extensions: {string.Join(separator: ", ", imageLoader.SupportedExtensions)}");

                    await ProcessGalleryAsync(imageSettings, imageLoader);

                    retval = 0;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(format: "Error: {0}", exception.Message);
                    Console.WriteLine(format: "Stack Trace: {0}", exception.StackTrace);

                    retval = 1;
                }

                await DumpBrokenImagesAsync();

                ret = retval;
            }

            return ret;
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

            ImageLoaderCoreSetup.Configure(serviceCollection);
            ImageLoaderStandardSetup.Configure(serviceCollection);
            ImageLoaderRawSetup.Configure(serviceCollection);
            ImageLoaderPhotoshopSetup.Configure(serviceCollection);

            return serviceCollection;
        }

        private static Task DumpBrokenImagesAsync()
        {
            string[] images = BrokenImages.AllBrokenImages();

            File.WriteAllLines(Settings.BrokenImagesFile, images, Encoding.UTF8);

            return ConsoleOutput.LineAsync(formatString: "Broken Images: {0}", images.Length);
        }

        private static async Task<HashSet<string>> ProcessAsync(IImageLoader imageLoader, Photo[] source, Photo[] target, ISettings imageSettings)
        {
            ConcurrentDictionary<string, bool> items = new ConcurrentDictionary<string, bool>();

            await Task.WhenAll(source.Select(selector: sourcePhoto => ProcessSinglePhotoAsync(imageLoader, target, sourcePhoto, items, imageSettings))
                                     .ToArray());

            return new HashSet<string>(items.Keys);
        }

        private static async Task ProcessGalleryAsync(ISettings imageSettings, IImageLoader imageLoader)
        {
            Task<Photo[]> sourceTask = PhotoMetadataRepository.LoadEmptyRepositoryAsync(Settings.RootFolder);
            Task<Photo[]> targetTask = PhotoMetadataRepository.LoadRepositoryAsync(Settings.DatabaseOutputFolder);

            Photo[][] results = await Task.WhenAll(sourceTask, targetTask);

            Photo[] source = results[0];
            Photo[] target = results[1];

            await ProcessAsync(imageLoader, source, target, imageSettings);
        }

        private static async Task ProcessOneFileAsync(IImageLoader imageLoader,
                                                      Photo sourcePhoto,
                                                      Photo targetPhoto,
                                                      bool rebuild,
                                                      bool rebuildMetadata,
                                                      string url,
                                                      string shortUrl,
                                                      ISettings imageSettings)
        {
            await ConsoleOutput.LineAsync(rebuild ? "Rebuild: {0}" : "Build: {0}", sourcePhoto.UrlSafePath);

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
                await ConsoleOutput.LineAsync(formatString: "Build images:");
                DateTime creationDate = MetadataHelpers.ExtractCreationDate(sourcePhoto.Metadata);

                try
                {
                    sourcePhoto.ImageSizes = await ImageExtraction.BuildImagesAsync(imageLoader, sourcePhoto, filesCreated, creationDate, url, shortUrl, imageSettings);
                }
                catch (Exception exception)
                {
                    await ConsoleOutput.LineAsync($" Failed to load image: {sourcePhoto.UrlSafePath}: {exception.Message}");

                    throw;
                }
            }
            else
            {
                await ConsoleOutput.LineAsync(formatString: "Not building images");
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
                                                          ISettings imageSettings)
        {
            ForceGarbageCollection();

            try
            {
                Photo targetPhoto = target.FirstOrDefault(predicate: item => item.PathHash == sourcePhoto.PathHash);
                bool build = targetPhoto == null;
                bool rebuild = targetPhoto != null && await RebuildDetection.NeedsFullResizedImageRebuildAsync(sourcePhoto, targetPhoto, imageSettings);
                bool rebuildMetadata = targetPhoto != null && await RebuildDetection.MetadataVersionOutOfDateAsync(targetPhoto);

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
                            await ConsoleOutput.LineAsync(formatString: " +++ Force rebuild: missing shortcut URL.  New short url: {0}", shortUrl);
                        }
                    }
                }
                else
                {
                    if (ShortUrls.TryGetValue(url, out shortUrl) && !string.IsNullOrWhiteSpace(shortUrl))
                    {
                        await ConsoleOutput.LineAsync(formatString: "* Reusing existing short url: {0}", shortUrl);
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
                    await ProcessOneFileAsync(imageLoader, sourcePhoto, targetPhoto, rebuild, rebuildMetadata, url, shortUrl, imageSettings);
                }
                else
                {
                    await ConsoleOutput.LineAsync(formatString: "Unchanged: {0}", targetPhoto.UrlSafePath);
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

                    await FileHelpers.WriteAllBytesAsync(filename, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tracking.ToArray())), false);
                }
                else
                {
                    if (counter.Impressions < maxImpressionsPerMonth)
                    {
                        Console.WriteLine(format: "Bitly Impressions for {0}", counter.Impressions);
                        Console.WriteLine(format: "Bitly Impressions total {0}", counter.TotalImpressionsEver);
                        ++counter.Impressions;
                        ++counter.TotalImpressionsEver;

                        await FileHelpers.WriteAllBytesAsync(filename, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tracking.ToArray())), false);
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