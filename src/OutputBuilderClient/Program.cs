﻿using System;
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
using Images.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OutputBuilderClient.Interfaces;
using OutputBuilderClient.Services;
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

            ServiceCollection serviceCollection = RegisterServices(args);

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            loggerFactory.AddSerilog();

            ILogger logging = loggerFactory.CreateLogger(categoryName: "OutputBuilderClient");

            ISettings settings = serviceProvider.GetService<ISettings>();
            IImageSettings imageSettings = serviceProvider.GetService<IImageSettings>();

            logging.LogInformation($"Source: {settings.RootFolder}");
            logging.LogInformation($"Output: {settings.DatabaseOutputFolder}");
            logging.LogInformation($"Images: {settings.ImagesOutputPath}");
            logging.LogInformation($"Thumb:  {imageSettings.ThumbnailSize}");

            foreach (int resize in imageSettings.ImageMaximumDimensions)
            {
                logging.LogInformation($"Resize: {resize}");
            }

            try
            {
                IShortUrls shortUrls = serviceProvider.GetService<IShortUrls>();

                await shortUrls.LoadAsync();

                IImageLoader imageLoader = serviceProvider.GetService<IImageLoader>();

                logging.LogInformation($"Supported Extensions: {string.Join(separator: ", ", imageLoader.SupportedExtensions)}");

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
                IBrokenImageTracker brokenImageTracker = serviceProvider.GetService<IBrokenImageTracker>();

                await DumpBrokenImagesAsync(brokenImageTracker, settings, logging);
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

        private static ServiceCollection RegisterServices(string[] args)
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

            serviceCollection.AddSingleton<ILimitedUrlShortener, LimitedUrlShortenerer>();
            serviceCollection.AddSingleton<IShortUrls, ShortUrls>();
            serviceCollection.AddSingleton<IImageFilenameGeneration, ImageFilenameGeneration>();
            serviceCollection.AddSingleton<IImageExtraction, ImageExtraction>();
            serviceCollection.AddSingleton<IRebuildDetection, RebuildDetection>();
            serviceCollection.AddSingleton<ISourceImageFileLocator, SourceImageFileLocator>();
            serviceCollection.AddSingleton<IResizeImageFileLocator, ResizeImageFileLocator>();
            serviceCollection.AddSingleton<IGalleryBuilder, GalleryBuilder>();

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

            ISettings settings = new Settings(rootFolder: config.GetValue<string>(rootFolder),
                                              databaseOutputFolder: config.GetValue<string>(databaseOutputFolder),
                                              imagesOutputPath: config.GetValue<string>(outputImages),
                                              shortNamesFile: config.GetValue<string>(outputShortUrls),
                                              brokenImagesFile: config.GetValue<string>(outputBrokenImages),
                                              bitlyApiUser: config.GetValue<string>(key: @"UrlShortener:BitlyApiUser"),
                                              bitlyApiKey: config.GetValue<string>(key: @"UrlShortener:BitlyApiKey"));
            IImageSettings imageImageSettings = new ImageSettings(thumbnailSize: config.GetValue(outputThumbnailSize, defaultValue: 150),
                                                                  shortUrlsPath: settings.ShortNamesFile,
                                                                  defaultShortUrl: @"https://www.markridgwell.co.uk",
                                                                  imageMaximumDimensions: config.GetValue(outputMaximumDimensions, defaultValue: @"400,600,800,1024,1600"),
                                                                  jpegOutputQuality: config.GetValue(outputJpegQuality, defaultValue: 100),
                                                                  watermarkImage: config.GetValue<string>(watermark));

            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton(imageImageSettings);

            return serviceCollection;
        }

        private static async Task DumpBrokenImagesAsync(IBrokenImageTracker brokenImageTracker, ISettings settings, ILogger logging)
        {
            string[] images = brokenImageTracker.AllBrokenImages();

            await File.WriteAllLinesAsync(settings.BrokenImagesFile, images, Encoding.UTF8);

            logging.LogInformation($"Broken Images: {images.Length}");
        }
    }
}