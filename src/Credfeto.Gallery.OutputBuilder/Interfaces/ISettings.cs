namespace Credfeto.Gallery.OutputBuilder.Interfaces
{
    public interface ISettings
    {
        string RootFolder { get; }

        string BitlyApiKey { get; }

        string BitlyApiUser { get; }

        string DatabaseOutputFolder { get; }

        public string ImagesOutputPath { get; }

        string BrokenImagesFile { get; }

        string ShortNamesFile { get; }
    }
}