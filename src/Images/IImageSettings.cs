using System.Collections.Generic;

namespace Images
{
    public interface IImageSettings
    {
        int ThumbnailSize { get; }

        IReadOnlyList<int> ImageMaximumDimensions { get; }

        long JpegOutputQuality { get; }

        string WatermarkImage { get; }
    }
}