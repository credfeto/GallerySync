namespace Images
{
    public interface ISettings
    {
        int ThumbnailSize { get; }
        string ImageMaximumDimensions { get; }
        string RootFolder { get; }
        string ImagesOutputPath { get; }
        long JpegOutputQuality { get; }
        string WatermarkImage { get; set; }
        object DefaultShortUrl { get; }
    }
}