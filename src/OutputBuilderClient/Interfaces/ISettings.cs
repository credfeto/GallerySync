namespace OutputBuilderClient.Interfaces
{
    public interface ISettings
    {
        string RootFolder { get; }

        string BitlyApiKey { get; }

        string BitlyApiUser { get; }

        string DatabaseOutputFolder { get; }

        string BrokenImagesFile { get; }

        string ShortNamesFile { get; }
    }
}