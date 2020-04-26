using System.IO;
using Images;
using ObjectModel;
using OutputBuilderClient.Interfaces;

namespace OutputBuilderClient.Services
{
    public sealed class SourceImageFileLocator : ISourceImageFileLocator
    {
        private readonly ISettings _settings;

        public SourceImageFileLocator(ISettings settings)
        {
            this._settings = settings;
        }

        public string GetFilename(Photo sourcePhoto)
        {
            return Path.Combine(this._settings.RootFolder, sourcePhoto.BasePath + sourcePhoto.ImageExtension);
        }
    }
}