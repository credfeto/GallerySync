using Images;

namespace OutputBuilderClient
{
    internal class ImageSettings : ISettings
    {
        public ImageSettings(string defaultShortUrl,
                             int thumbnailSize,
                             string imageMaximumDimensions,
                             string rootFolder,
                             string imagesOutputPath,
                             long jpegOutputQuality,
                             string watermarkImage)
        {
            this.DefaultShortUrl = defaultShortUrl;
            this.ThumbnailSize = thumbnailSize;
            this.ImageMaximumDimensions = imageMaximumDimensions;
            this.RootFolder = rootFolder;
            this.ImagesOutputPath = imagesOutputPath;
            this.JpegOutputQuality = jpegOutputQuality;
            this.WatermarkImage = watermarkImage;
        }

        public int ThumbnailSize { get; }

        public string ImageMaximumDimensions { get; }

        public string RootFolder { get; }

        public string ImagesOutputPath { get; }

        public long JpegOutputQuality { get; }

        public string WatermarkImage { get; set; }

        public object DefaultShortUrl { get; }
    }
}