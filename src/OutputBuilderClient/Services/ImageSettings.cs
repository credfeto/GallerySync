using System;
using System.Collections.Generic;
using System.Linq;
using Images;

namespace OutputBuilderClient.Services
{
    internal class ImageSettings : IImageSettings
    {
        public ImageSettings(string defaultShortUrl, int thumbnailSize, string imageMaximumDimensions, long jpegOutputQuality, string watermarkImage, string shortUrlsPath)
        {
            this.DefaultShortUrl = defaultShortUrl;
            this.ThumbnailSize = thumbnailSize;
            this.ImageMaximumDimensions = imageMaximumDimensions.Split(separator: ',')
                                                                .Select(selector: value => Convert.ToInt32(value))
                                                                .Distinct()
                                                                .ToArray();
            this.JpegOutputQuality = jpegOutputQuality;
            this.WatermarkImage = watermarkImage;
            this.ShortUrlsPath = shortUrlsPath;
        }

        public string RootFolder { get; }

        public string ImagesOutputPath { get; }

        public string ShortUrlsPath { get; }

        public object DefaultShortUrl { get; }

        public int ThumbnailSize { get; }

        public IReadOnlyList<int> ImageMaximumDimensions { get; }

        public long JpegOutputQuality { get; }

        public string WatermarkImage { get; set; }
    }
}