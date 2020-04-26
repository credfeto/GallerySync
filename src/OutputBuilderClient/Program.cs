using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ImageLoader.Interfaces;
using Images;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OutputBuilderClient.Interfaces;
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

            ServiceCollection serviceCollection = Startup.RegisterServices(args);

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

        private static async Task DumpBrokenImagesAsync(IBrokenImageTracker brokenImageTracker, ISettings settings, ILogger logging)
        {
            string[] images = brokenImageTracker.AllBrokenImages();

            await File.WriteAllLinesAsync(settings.BrokenImagesFile, images, Encoding.UTF8);

            logging.LogInformation($"Broken Images: {images.Length}");
        }
    }
}