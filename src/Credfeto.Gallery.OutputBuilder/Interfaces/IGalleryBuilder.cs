using System.Threading.Tasks;
using Credfeto.Gallery.Image;

namespace Credfeto.Gallery.OutputBuilder.Interfaces;

public interface IGalleryBuilder
{
    public Task ProcessGalleryAsync(IImageSettings imageImageSettings);
}