using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace OutputBuilderClient
{
    internal static class Program
    {
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

                IGalleryBuilder galleryBuilder = serviceProvider.GetService<IGalleryBuilder>();

                await galleryBuilder.ProcessGalleryAsync(imageSettings);

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

            serviceCollection.AddSingleton<IImageFilenameGeneration, ImageFilenameGeneration>();
            serviceCollection.AddSingleton<IImageExtraction, ImageExtraction>();
            serviceCollection.AddSingleton<IRebuildDetection, RebuildDetection>();

            serviceCollection.AddSingleton<IGalleryBuilder, GalleryBuilder>();

            return serviceCollection;
        }

        private static async Task DumpBrokenImagesAsync(ILogger logging)
        {
            string[] images = BrokenImages.AllBrokenImages();

            await File.WriteAllLinesAsync(Settings.BrokenImagesFile, images, Encoding.UTF8);

            logging.LogInformation($"Broken Images: {images.Length}");
        }
    }
}