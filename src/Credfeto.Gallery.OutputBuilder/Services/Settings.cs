using Credfeto.Gallery.OutputBuilder.Interfaces;

namespace Credfeto.Gallery.OutputBuilder.Services
{
    public sealed class Settings : ISettings
    {
        public Settings(string bitlyApiKey, string bitlyApiUser, string rootFolder, string databaseOutputFolder, string imagesOutputPath, string brokenImagesFile, string shortNamesFile)
        {
            this.BitlyApiKey = bitlyApiKey;
            this.BitlyApiUser = bitlyApiUser;
            this.RootFolder = rootFolder;
            this.DatabaseOutputFolder = databaseOutputFolder;
            this.ImagesOutputPath = imagesOutputPath;
            this.BrokenImagesFile = brokenImagesFile;
            this.ShortNamesFile = shortNamesFile;
        }

        public string RootFolder { get; }

        public string BitlyApiKey { get; }

        public string BitlyApiUser { get; }

        public string DatabaseOutputFolder { get; }

        public string ImagesOutputPath { get; }

        public string BrokenImagesFile { get; }

        public string ShortNamesFile { get; }
    }
}