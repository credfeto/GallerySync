using Credfeto.Gallery.ObjectModel;

namespace Credfeto.Gallery.Image
{
    public interface IResizeImageFileLocator
    {
        string GetResizedFileName(Photo sourcePhoto, ImageSize resized);

        string GetResizedFileName(Photo sourcePhoto, ImageSize resized, string extension);
    }
}