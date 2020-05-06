using System.IO;
using Credfeto.Gallery.Image;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.OutputBuilder.Interfaces;

namespace Credfeto.Gallery.OutputBuilder.Services
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
            return Path.Combine(path1: this._settings.RootFolder, sourcePhoto.BasePath + sourcePhoto.ImageExtension);
        }
    }
}