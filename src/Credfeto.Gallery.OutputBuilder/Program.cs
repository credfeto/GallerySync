using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Credfeto.Gallery.Image;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Credfeto.ImageLoader.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Credfeto.Gallery.OutputBuilder;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine(value: "Credfeto.Gallery.OutputBuilder");

        AlterPriority();

        ServiceCollection serviceCollection = Startup.RegisterServices(args);

        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        loggerFactory.AddSerilog();

        ILogger logging = loggerFactory.CreateLogger(categoryName: "Credfeto.Gallery.OutputBuilder");

        ISettings settings = serviceProvider.GetService<ISettings>();
        IImageSettings imageSettings = serviceProvider.GetService<IImageSettings>();

        logging.LogInformation($"Source: {settings.RootFolder}");
        logging.LogInformation($"Output: {settings.DatabaseOutputFolder}");
        logging.LogInformation($"Credfeto.Gallery.Image: {settings.ImagesOutputPath}");
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

            logging.LogInformation($"Supported Extensions: {string.Join(separator: ", ", values: imageLoader.SupportedExtensions)}");

            IGalleryBuilder galleryBuilder = serviceProvider.GetService<IGalleryBuilder>();

            await galleryBuilder.ProcessGalleryAsync(imageSettings);

            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine(format: "Error: {0}", arg0: exception.Message);
            Console.WriteLine(format: "Stack Trace: {0}", arg0: exception.StackTrace);

            return 1;
        }
        finally
        {
            IBrokenImageTracker brokenImageTracker = serviceProvider.GetService<IBrokenImageTracker>();

            await DumpBrokenImagesAsync(brokenImageTracker: brokenImageTracker, settings: settings, logging: logging);
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

    private static async Task DumpBrokenImagesAsync(IBrokenImageTracker brokenImageTracker, ISettings settings, ILogger logging)
    {
        string[] images = brokenImageTracker.AllBrokenImages();

        await File.WriteAllLinesAsync(path: settings.BrokenImagesFile, contents: images, encoding: Encoding.UTF8);

        logging.LogInformation($"Broken Credfeto.Gallery.Image: {images.Length}");
    }
}