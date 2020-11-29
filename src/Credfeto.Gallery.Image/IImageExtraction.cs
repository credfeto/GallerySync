using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Credfeto.Gallery.ObjectModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Credfeto.Gallery.Image
{
    public interface IImageExtraction
    {
        Task<IReadOnlyList<ImageSize>> BuildImagesAsync(Photo sourcePhoto,
                                                        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1002:DoNotExposeGenericLists", Justification = "Existing API")]
                                                        List<string> filesCreated,
                                                        DateTime creationDate,
                                                        string url,
                                                        string shortUrl,
                                                        IImageSettings imageSettings);

        /// <summary>
        ///     Saves the image as a block of JPEG bytes in memory.
        /// </summary>
        /// <param name="image">
        ///     The image.
        /// </param>
        /// <param name="compressionQuality">
        ///     The compression quality.
        /// </param>
        /// <param name="url"></param>
        /// <param name="shortUrl"></param>
        /// <param name="metadata"></param>
        /// <param name="creationDate"></param>
        /// <returns>
        ///     Block of bytes representing the image.
        /// </returns>
        byte[] SaveImageAsJpegBytes(Image<Rgba32> image,
                                    long compressionQuality,
                                    string url,
                                    string shortUrl,
                                    IReadOnlyList<PhotoMetadata> metadata,
                                    DateTime creationDate,
                                    IImageSettings imageSettings);

        /// <summary>
        ///     Writes the image.
        /// </summary>
        /// <param name="fileName">
        ///     Name of the file.
        /// </param>
        /// <param name="data">
        ///     The data to write to the file.
        /// </param>
        /// <param name="creationDate"></param>
        Task WriteImageAsync(string fileName, byte[] data, DateTime creationDate);
    }
}