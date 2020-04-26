using ObjectModel;

namespace Images
{
    public interface ISourceImageFileLocator
    {
        string GetFilename(Photo sourcePhoto);
    }
}