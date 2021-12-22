using Credfeto.Gallery.ObjectModel;

namespace Credfeto.Gallery.Image;

public interface ISourceImageFileLocator
{
    string GetFilename(Photo sourcePhoto);
}