using ObjectModel;

namespace Images
{
    public interface IResizeImageFileLocator
    {
        string GetResizedFileName(Photo sourcePhoto, ImageSize resized);

        string GetResizedFileName(Photo sourcePhoto, ImageSize resized, string extension);
    }
}