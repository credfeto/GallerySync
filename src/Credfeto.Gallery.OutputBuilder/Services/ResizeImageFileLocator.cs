using System.IO;
using FileNaming;
using Images;
using ObjectModel;
using OutputBuilderClient.Interfaces;

namespace OutputBuilderClient.Services
{
    public sealed class ResizeImageFileLocator : IResizeImageFileLocator
    {
        private readonly IImageFilenameGeneration _imageFilenameGeneration;
        private readonly ISettings _settings;

        public ResizeImageFileLocator(ISettings settings, IImageFilenameGeneration imageFilenameGeneration)
        {
            this._settings = settings;
            this._imageFilenameGeneration = imageFilenameGeneration;
        }

        public string GetResizedFileName(Photo sourcePhoto, ImageSize resized)
        {
            return Path.Combine(this._settings.ImagesOutputPath,
                                HashNaming.PathifyHash(sourcePhoto.PathHash),
                                this._imageFilenameGeneration.IndividualResizeFileName(sourcePhoto, resized));
        }

        public string GetResizedFileName(Photo sourcePhoto, ImageSize resized, string extension)
        {
            return Path.Combine(this._settings.ImagesOutputPath,
                                HashNaming.PathifyHash(sourcePhoto.PathHash),
                                this._imageFilenameGeneration.IndividualResizeFileName(sourcePhoto, resized, extension));
        }
    }
}