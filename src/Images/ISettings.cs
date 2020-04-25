using System.Collections.Generic;

namespace Images
{
    public interface ISettings
    {
        int ThumbnailSize { get; }

        IReadOnlyList<int> ImageMaximumDimensions { get; }

        string RootFolder { get; }

        string ImagesOutputPath { get; }

        long JpegOutputQuality { get; }

        string WatermarkImage { get; set; }

        object DefaultShortUrl { get; }

        string ShortUrlsPath { get; }
    }
}