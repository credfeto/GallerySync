using System.Collections.Generic;
using System.IO;
using Credfeto.Gallery.Image;
using Credfeto.Gallery.Image.Services;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Credfeto.Gallery.OutputBuilder.Services;
using ImageLoader.Core;
using ImageLoader.Photoshop;
using ImageLoader.Raw;
using ImageLoader.Standard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Credfeto.Gallery.OutputBuilder
{
    internal static class Startup
    {
        public static ServiceCollection RegisterServices(string[] args)
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

            BitlyUrlShortner.RegisterServices(serviceCollection);
            serviceCollection.AddSingleton<ILimitedUrlShortener, LimitedUrlShortenerer>();
            serviceCollection.AddSingleton<IShortUrls, ShortUrls>();
            serviceCollection.AddSingleton<IImageFilenameGeneration, ImageFilenameGeneration>();
            serviceCollection.AddSingleton<IImageExtraction, ImageExtraction>();
            serviceCollection.AddSingleton<IRebuildDetection, RebuildDetection>();
            serviceCollection.AddSingleton<ISourceImageFileLocator, SourceImageFileLocator>();
            serviceCollection.AddSingleton<IResizeImageFileLocator, ResizeImageFileLocator>();
            serviceCollection.AddSingleton<IBrokenImageTracker, BrokenImageTracker>();
            serviceCollection.AddSingleton<IGalleryBuilder, GalleryBuilder>();

            const string rootFolder = @"Source:RootFolder";

            const string databaseOutputFolder = @"Database:OutputFolder";
            const string outputShortUrls = @"Output:ShortUrls";
            const string outputImages = @"Output:ImagesOutputPath";
            const string outputBrokenImages = @"Output:BrokenImagesFile";
            const string watermark = @"Credfeto.Gallery.Image:Watermark";

            const string outputJpegQuality = @"Output:JpegOutputQuality";
            const string outputMaximumDimensions = @"Output:ImageMaximumDimensions";
            const string outputThumbnailSize = @"Output:ThumbnailSize";

            IConfigurationRoot config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                                                                  .AddJsonFile(path: "appsettings.json", optional: true)
                                                                  .AddCommandLine(args: args,
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
            IImageSettings imageImageSettings = new ImageSettings(thumbnailSize: config.GetValue(key: outputThumbnailSize, defaultValue: 150),
                                                                  shortUrlsPath: settings.ShortNamesFile,
                                                                  defaultShortUrl: @"https://www.markridgwell.co.uk",
                                                                  imageMaximumDimensions: config.GetValue(key: outputMaximumDimensions, defaultValue: @"400,600,800,1024,1600"),
                                                                  jpegOutputQuality: config.GetValue(key: outputJpegQuality, defaultValue: 100),
                                                                  watermarkImage: config.GetValue<string>(watermark));

            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton(imageImageSettings);

            return serviceCollection;
        }
    }
}