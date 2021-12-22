using System.Threading.Tasks;
using Credfeto.Gallery.Image;
using Credfeto.Gallery.ObjectModel;
using Microsoft.Extensions.Logging;

namespace Credfeto.Gallery.OutputBuilder.Interfaces;

public interface IRebuildDetection
{
    Task<bool> NeedsFullResizedImageRebuildAsync(Photo sourcePhoto, Photo targetPhoto, IImageSettings imageImageSettings, ILogger logging);

    bool MetadataVersionOutOfDate(Photo targetPhoto, ILogger logging);

    Task<bool> HaveFilesChangedAsync(Photo sourcePhoto, Photo targetPhoto, ILogger logging);

    bool MetadataVersionRequiresRebuild(Photo targetPhoto, ILogger logging);
}