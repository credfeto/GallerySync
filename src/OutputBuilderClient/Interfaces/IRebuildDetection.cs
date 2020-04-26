using System.Threading.Tasks;
using Images;
using Microsoft.Extensions.Logging;
using ObjectModel;

namespace OutputBuilderClient.Interfaces
{
    public interface IRebuildDetection
    {
        Task<bool> NeedsFullResizedImageRebuildAsync(Photo sourcePhoto, Photo targetPhoto, IImageSettings imageImageSettings, ILogger logging);

        bool MetadataVersionOutOfDate(Photo targetPhoto, ILogger logging);

        Task<bool> HaveFilesChangedAsync(Photo sourcePhoto, Photo targetPhoto, ILogger logging);

        bool MetadataVersionRequiresRebuild(Photo targetPhoto, ILogger logging);
    }
}