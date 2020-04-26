using System.Collections.Generic;

namespace Credfeto.Gallery.Image
{
    public interface IImageSettings
    {
        int ThumbnailSize { get; }

        IReadOnlyList<int> ImageMaximumDimensions { get; }

        long JpegOutputQuality { get; }

        string WatermarkImage { get; }
    }
}