using System.IO;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Credfeto.Gallery.Image.Services
{
    public sealed class ImageFilenameGeneration : IImageFilenameGeneration
    {
        public string IndividualResizeFileName(Photo sourcePhoto, Image<Rgba32> resized)
        {
            return this.IndividualResizeFileName(sourcePhoto, resized, extension: @"jpg");
        }

        public string IndividualResizeFileName(Photo sourcePhoto, Image<Rgba32> resized, string extension)
        {
            string basePath = UrlNaming.BuildUrlSafePath(string.Format(format: "{0}-{1}x{2}", Path.GetFileName(sourcePhoto.BasePath), resized.Width, resized.Height))
                                       .TrimEnd(trimChar: '/')
                                       .TrimStart(trimChar: '-');

            return basePath + "." + extension;
        }

        public string IndividualResizeFileName(Photo sourcePhoto, ImageSize resized)
        {
            return this.IndividualResizeFileName(sourcePhoto, resized, extension: "jpg");
        }

        public string IndividualResizeFileName(Photo sourcePhoto, ImageSize resized, string extension)
        {
            string basePath = UrlNaming.BuildUrlSafePath(string.Format(format: "{0}-{1}x{2}", Path.GetFileName(sourcePhoto.BasePath), resized.Width, resized.Height))
                                       .TrimEnd(trimChar: '/')
                                       .TrimStart(trimChar: '-');

            return basePath + "." + extension;
        }
    }
}