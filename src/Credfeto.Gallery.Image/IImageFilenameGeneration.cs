using Credfeto.Gallery.ObjectModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Credfeto.Gallery.Image;

public interface IImageFilenameGeneration
{
    string IndividualResizeFileName(Photo sourcePhoto, Image<Rgba32> resized);

    string IndividualResizeFileName(Photo sourcePhoto, Image<Rgba32> resized, string extension);

    string IndividualResizeFileName(Photo sourcePhoto, ImageSize resized);

    string IndividualResizeFileName(Photo sourcePhoto, ImageSize resized, string extension);
}