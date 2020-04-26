using System;
using System.Collections.Generic;
using System.Linq;
using Images;

namespace OutputBuilderClient.Interfaces
{
    internal class ImageSettings : ISettings
    {
        public ImageSettings(string defaultShortUrl,
                             int thumbnailSize,
                             string imageMaximumDimensions,
                             string rootFolder,
                             string imagesOutputPath,
                             long jpegOutputQuality,
                             string watermarkImage,
                             string shortUrlsPath)
        {
            this.DefaultShortUrl = defaultShortUrl;
            this.ThumbnailSize = thumbnailSize;
            this.ImageMaximumDimensions = imageMaximumDimensions.Split(separator: ',')
                                                                .Select(selector: value => Convert.ToInt32(value))
                                                                .Distinct()
                                                                .ToArray();
            this.RootFolder = rootFolder;
            this.ImagesOutputPath = imagesOutputPath;
            this.JpegOutputQuality = jpegOutputQuality;
            this.WatermarkImage = watermarkImage;
            this.ShortUrlsPath = shortUrlsPath;
        }

        public int ThumbnailSize { get; }

        public IReadOnlyList<int> ImageMaximumDimensions { get; }

        public string RootFolder { get; }

        public string ImagesOutputPath { get; }

        public long JpegOutputQuality { get; }

        public string WatermarkImage { get; set; }

        public string ShortUrlsPath { get; }

        public object DefaultShortUrl { get; }
    }
}