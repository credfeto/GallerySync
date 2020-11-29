using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.Storage;
using Credfeto.ImageLoader.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Credfeto.Gallery.Image.Services
{
    public sealed class ImageExtraction : IImageExtraction
    {
        private readonly IImageFilenameGeneration _imageFilenameGeneration;
        private readonly IImageLoader _imageLoader;
        private readonly ILogger<ImageExtraction> _logging;
        private readonly IResizeImageFileLocator _resizeImageFileLocator;
        private readonly ISourceImageFileLocator _sourceImageFileLocator;

        public ImageExtraction(IImageLoader imageLoader,
                               IImageFilenameGeneration imageFilenameGeneration,
                               ISourceImageFileLocator sourceImageFileLocator,
                               IResizeImageFileLocator resizeImageFileLocator,
                               ILogger<ImageExtraction> logging)
        {
            this._imageLoader = imageLoader;
            this._imageFilenameGeneration = imageFilenameGeneration;
            this._sourceImageFileLocator = sourceImageFileLocator;
            this._resizeImageFileLocator = resizeImageFileLocator;
            this._logging = logging;
        }

        /// <summary>
        ///     Gets the copyright declaration.
        /// </summary>
        /// <returns>
        ///     The copyright declaration.
        /// </returns>
        private static string CopyrightDeclaration
        {
            get
            {
                Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));

                return "Copyright (c) Mark Ridgwell (https://www.markridgwell.co.uk/about) All Rights Reserved\0";
            }
        }

        public async Task<IReadOnlyList<ImageSize>> BuildImagesAsync(Photo sourcePhoto, List<string> filesCreated, DateTime creationDate, string url, string shortUrl, IImageSettings imageSettings)
        {
            string rawExtension = sourcePhoto.ImageExtension.TrimStart(trimChar: '.')
                                             .ToUpperInvariant();

            string filename = this._sourceImageFileLocator.GetFilename(sourcePhoto);

            if (!this._imageLoader.CanLoad(filename))
            {
                this._logging.LogError($"No image converter for {rawExtension}");

                return Array.Empty<ImageSize>();
            }

            List<ImageSize> sizes = new();

            IReadOnlyList<int> imageSizes = StandardImageSizesWithThumbnailSize(imageSettings);

            this._logging.LogInformation($"Loading image: {filename}");

            using (Image<Rgba32> sourceBitmap = await this._imageLoader.LoadImageAsync(filename))
            {
                if (sourceBitmap == null)
                {
                    this._logging.LogWarning($"Could not load : {filename}");

                    return null;
                }

                this._logging.LogInformation($"Loaded: {filename}");

                int sourceImageWidth = sourceBitmap.Width;
                this._logging.LogInformation($"Using Image Width: {sourceBitmap.Width}");

                foreach (int dimension in imageSizes.Where(predicate: size => ResziedImageWillNotBeBigger(size: size, sourceImageWidth: sourceImageWidth)))
                {
                    this._logging.LogDebug($"Creating Dimension: {dimension}");

                    using (Image<Rgba32> resized = ResizeImage(image: sourceBitmap, maximumDimension: dimension))
                    {
                        string resizedFileName = this._resizeImageFileLocator.GetResizedFileName(sourcePhoto: sourcePhoto, new ImageSize {Width = resized.Width, Height = resized.Height});

                        ApplyWatermark(imageToAddWatermarkTo: resized, url: shortUrl, imageSettings: imageSettings);

                        long quality = imageSettings.JpegOutputQuality;
                        byte[] resizedBytes = this.SaveImageAsJpegBytes(image: resized,
                                                                        compressionQuality: quality,
                                                                        url: url,
                                                                        shortUrl: shortUrl,
                                                                        metadata: sourcePhoto.Metadata,
                                                                        creationDate: creationDate,
                                                                        imageSettings: imageSettings);

                        if (!ImageHelpers.IsValidJpegImage(bytes: resizedBytes, "In memory image to be saved as: " + resizedFileName))
                        {
                            throw new AbortProcessingException(string.Format(format: "File {0} produced an invalid image", arg0: filename));
                        }

                        await this.WriteImageAsync(fileName: resizedFileName, data: resizedBytes, creationDate: creationDate);

                        byte[] resizedData = await File.ReadAllBytesAsync(resizedFileName);

                        if (!ImageHelpers.IsValidJpegImage(bytes: resizedData, "Saved resize image: " + resizedFileName))
                        {
                            this._logging.LogError($"Error: File {resizedFileName} produced an invalid image");

                            throw new AbortProcessingException(string.Format(format: "File {0} produced an invalid image", arg0: filename));
                        }

                        filesCreated.Add(HashNaming.PathifyHash(sourcePhoto.PathHash) + "\\" + this._imageFilenameGeneration.IndividualResizeFileName(sourcePhoto: sourcePhoto, resized: resized));

                        if (resized.Width == imageSettings.ThumbnailSize)
                        {
                            resizedFileName = this._resizeImageFileLocator.GetResizedFileName(sourcePhoto: sourcePhoto,
                                                                                              new ImageSize {Width = resized.Width, Height = resized.Height},
                                                                                              extension: "png");
                            resizedBytes = SaveImageAsPng(image: resized, url: url, metadata: sourcePhoto.Metadata, creationDate: creationDate);
                            await this.WriteImageAsync(fileName: resizedFileName, data: resizedBytes, creationDate: creationDate);

                            filesCreated.Add(HashNaming.PathifyHash(sourcePhoto.PathHash) + "\\" +
                                             this._imageFilenameGeneration.IndividualResizeFileName(sourcePhoto: sourcePhoto, resized: resized, extension: "png"));
                        }

                        sizes.Add(new ImageSize {Width = resized.Width, Height = resized.Height});
                    }
                }
            }

            return sizes;
        }

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
        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Is fallback position where it retries.")]
        public byte[] SaveImageAsJpegBytes(Image<Rgba32> image,
                                           long compressionQuality,
                                           string url,
                                           string shortUrl,
                                           IReadOnlyList<PhotoMetadata> metadata,
                                           DateTime creationDate,
                                           IImageSettings imageSettings)
        {
            Contract.Requires(image != null);
            Contract.Requires(compressionQuality > 0);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            SetMetadataProperties(image: image, url: url, metadata: metadata, creationDate: creationDate);

            try
            {
                return SaveImageAsJpegBytesWithOptions(image: image, compression: compressionQuality);
            }
            catch
            {
                // Something failed, retry using the standard
                return SaveImageAsJpegBytesWithoutOptions(image);
            }
        }

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
        public async Task WriteImageAsync(string fileName, byte[] data, DateTime creationDate)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));
            Contract.Requires(data != null);

            Console.WriteLine($"---> Output to {fileName}");
            EnsureFolderExistsForFile(fileName);

            await FileHelpers.WriteAllBytesAsync(fileName: fileName, bytes: data, commit: true);

            MetadataOutput.SetCreationDate(fileName: fileName, creationDate: creationDate);
        }

        /// <summary>
        ///     Applies the watermark to the image.
        /// </summary>
        /// <param name="imageToAddWatermarkTo">
        ///     The image to add the watermark to.
        /// </param>
        /// <param name="url"></param>
        private static void ApplyWatermark(Image<Rgba32> imageToAddWatermarkTo, string url, IImageSettings imageSettings)
        {
            Contract.Requires(imageToAddWatermarkTo != null);

            const int spacer = 5;

            string watermarkFilename = imageSettings.WatermarkImage;

            if (string.IsNullOrEmpty(watermarkFilename))
            {
                return;
            }

            if (!File.Exists(watermarkFilename))
            {
                return;
            }

            using (Image<Rgba32> watermark = SixLabors.ImageSharp.Image.Load(watermarkFilename)
                                                      .CloneAs<Rgba32>())
            {
                watermark.Mutate(operation: pc => pc.BackgroundColor(Color.Transparent));

                int width = watermark.Width;
                int height = watermark.Height;

                using (Image<Rgba32> qr = QrCode.EncodeUrl(url))
                {
                    if (qr != null)
                    {
                        qr.Mutate(operation: px => px.BackgroundColor(Color.Transparent));

                        int qrWidth = qr.Width;
                        int qrHeight = qr.Height;

                        int qrXPos = imageToAddWatermarkTo.Width - (qrWidth + spacer);
                        int qrYPos = imageToAddWatermarkTo.Height - (qrHeight + spacer);

                        width = watermark.Width + (qrWidth > 0 ? qrWidth + 2 * spacer : 0);

                        int maxHeight = Math.Max(val1: watermark.Height, qrHeight + spacer);

                        if (imageToAddWatermarkTo.Width <= width || imageToAddWatermarkTo.Height <= maxHeight)
                        {
                            return;
                        }

                        imageToAddWatermarkTo.Mutate(operation: pc =>
                                                                {
                                                                    pc.DrawImage(image: qr,
                                                                                 new Point(x: qrXPos, y: qrYPos),
                                                                                 colorBlending: PixelColorBlendingMode.Overlay,
                                                                                 alphaComposition: PixelAlphaCompositionMode.SrcOver,
                                                                                 opacity: 1);
                                                                });
                    }
                }

                if (imageToAddWatermarkTo.Width <= width || imageToAddWatermarkTo.Height <= height)
                {
                    return;
                }

                int x = imageToAddWatermarkTo.Width - width;
                int y = imageToAddWatermarkTo.Height - height;

                imageToAddWatermarkTo.Mutate(operation: pc =>
                                                        {
                                                            pc.DrawImage(image: watermark,
                                                                         new Point(x: x, y: y),
                                                                         colorBlending: PixelColorBlendingMode.Overlay,
                                                                         alphaComposition: PixelAlphaCompositionMode.SrcOver,
                                                                         opacity: 1);
                                                        });
            }
        }

        /// <summary>
        ///     Calculates the scaled height from the width.
        /// </summary>
        /// <param name="scaleWidth">
        ///     Width of the scale.
        /// </param>
        /// <param name="originalWidth">
        ///     Width of the original.
        /// </param>
        /// <param name="originalHeight">
        ///     Height of the original.
        /// </param>
        /// <returns>
        ///     The height.
        /// </returns>
        private static int CalculateScaledHeightFromWidth(int scaleWidth, int originalWidth, int originalHeight)
        {
            Contract.Requires(scaleWidth > 0);
            Contract.Requires(originalWidth > 0);
            Contract.Requires(originalHeight > 0);
            Contract.Requires((int) (scaleWidth / (double) originalWidth * originalHeight) > 0);
            Contract.Ensures(Contract.Result<int>() > 0);

            return (int) (scaleWidth / (double) originalWidth * originalHeight);
        }

        private static void EnsureFolderExistsForFile(string fileName)
        {
            string folder = Path.GetDirectoryName(fileName);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        private static string ExtractDescription(IReadOnlyList<PhotoMetadata> metadata, string url, DateTime creationDate)
        {
            string description = string.Empty;
            PhotoMetadata desc = metadata.FirstOrDefault(predicate: item => StringComparer.InvariantCultureIgnoreCase.Equals(x: item.Name, y: MetadataNames.COMMENT));

            if (desc != null)
            {
                description = desc.Value;
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                description += ". ";
            }

            description += "Source : ";

            description += url;

            description += " Photo taken by Mark Ridgwell";

            if (creationDate != DateTime.MinValue)
            {
                description += " (" + creationDate.ToString(format: "yyyy-MM-dd") + ")";
            }

            description += ".";

            return description;
        }

        /// <summary>
        ///     Resizes the image.
        /// </summary>
        /// <param name="image">
        ///     The image to resize.
        /// </param>
        /// <param name="maximumDimension">
        ///     The maximumDimension of the new image.
        /// </param>
        /// <returns>
        ///     The resized image.
        /// </returns>
        private static Image<Rgba32> ResizeImage(Image<Rgba32> image, int maximumDimension)
        {
            Contract.Requires(image != null);
            Contract.Requires(image.Width > 0);
            Contract.Requires(image.Height > 0);
            Contract.Requires(maximumDimension > 0);
            Contract.Requires((int) (maximumDimension / (double) image.Width * image.Height) > 0);
            Contract.Ensures(Contract.Result<Image<Rgba32>>() != null);

            int yscale = CalculateScaledHeightFromWidth(scaleWidth: maximumDimension, originalWidth: image.Width, originalHeight: image.Height);

            Image<Rgba32> resized = null;

            try
            {
                resized = image.Clone(operation: x => x.Resize(width: maximumDimension, height: yscale));

                Debug.Assert(resized.Width == maximumDimension);
                Debug.Assert(resized.Height == yscale);

                return resized;
            }
            catch
            {
                resized?.Dispose();

                throw;
            }
        }

        private static bool ResziedImageWillNotBeBigger(int size, int sourceImageWidth)
        {
            // || size <= sourceImageHeight
            return size <= sourceImageWidth;
        }

        /// <summary>
        ///     Saves the image as a block of JPEG bytes in memory.
        /// </summary>
        /// <param name="image">
        ///     The image.
        /// </param>
        /// <param name="compression">
        ///     The compression quality.
        /// </param>
        /// <returns>
        ///     Block of bytes representing the image.
        /// </returns>
        private static byte[] SaveImageAsJpegBytesWithOptions(Image<Rgba32> image, long compression)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            using (MemoryStream ms = new())
            {
                JpegEncoder encoder = new() {Quality = (int) compression};

                image.SaveAsJpeg(stream: ms, encoder: encoder);

                return ms.ToArray();
            }

//            image.Quality = (int) compression;
//            return image.ToByteArray(MagickFormat.Jpeg);
        }

        /// <summary>
        ///     Saves the image as a block of JPEG bytes in memory.
        /// </summary>
        /// <param name="image">
        ///     The image.
        /// </param>
        /// <returns>
        ///     Block of bytes representing the image.
        /// </returns>
        private static byte[] SaveImageAsJpegBytesWithoutOptions(Image<Rgba32> image)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            using (MemoryStream ms = new())
            {
                JpegEncoder encoder = new() {Quality = 75};

                image.SaveAsJpeg(stream: ms, encoder: encoder);

                return ms.ToArray();
            }
        }

        private static byte[] SaveImageAsPng(Image<Rgba32> image, string url, IReadOnlyList<PhotoMetadata> metadata, DateTime creationDate)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            SetMetadataProperties(image: image, url: url, metadata: metadata, creationDate: creationDate);

            using (MemoryStream ms = new())
            {
                PngEncoder encoder = new() {CompressionLevel = PngCompressionLevel.BestCompression, Quantizer = new WuQuantizer(), ColorType = PngColorType.Palette};
                image.SaveAsPng(stream: ms, encoder: encoder);

                return ms.ToArray();
            }

//            var qSettings = new QuantizeSettings {Colors = 256, Dither = true, ColorSpace = ColorSpace.RGB};
//            image.Quantize(qSettings);
//
//
//
//            return image.ToByteArray(MagickFormat.Png8);
        }

        private static void SetExifMetadata(Image<Rgba32> image, DateTime creationDate, string description, string copyright, string licensing, string credit, string program)
        {
            ExifProfile exifProfile = image.Metadata.ExifProfile;

            if (exifProfile == null)
            {
                return;
            }

            MetadataOutput.SetCreationDate(creationDate: creationDate, exifProfile: exifProfile);

            MetadataOutput.SetDescription(description: description, exifProfile: exifProfile);
            MetadataOutput.SetCopyright(exifProfile: exifProfile, copyright: copyright);

            MetadataOutput.SetLicensing(exifProfile: exifProfile, licensing: licensing);

            MetadataOutput.SetPhotographer(exifProfile: exifProfile, credit: credit);

            MetadataOutput.SetProgram(exifProfile: exifProfile, program: program);
        }

        /// <summary>
        ///     Sets the copyright EXIF properties.
        /// </summary>
        /// <param name="image">
        ///     The image to set the properties to it.
        /// </param>
        /// <param name="url"></param>
        /// <param name="metadata"></param>
        /// <param name="creationDate"></param>
        private static void SetMetadataProperties(Image<Rgba32> image, string url, IReadOnlyList<PhotoMetadata> metadata, DateTime creationDate)
        {
            Contract.Requires(image != null);

            string copyright = CopyrightDeclaration;
            const string credit = "Camera owner, Mark Ridgwell; Photographer, Mark Ridgwell; Image creator, Mark Ridgwell";
            const string licensing = "For licensing information see https://www.markridgwell.co.uk/about/";
            const string program = "https://www.markridgwell.co.uk/";

            //string title = ExtractTitle(filePath, metadata);
            string description = ExtractDescription(metadata: metadata, url: url, creationDate: creationDate);

            SetExifMetadata(image: image, creationDate: creationDate, description: description, copyright: copyright, licensing: licensing, credit: credit, program: program);
        }

        private static IReadOnlyList<int> StandardImageSizesWithThumbnailSize(IImageSettings imageSettings)
        {
            return imageSettings.ImageMaximumDimensions.Concat(new[] {imageSettings.ThumbnailSize})
                                .Distinct()
                                .OrderByDescending(keySelector: x => x)
                                .ToArray();
        }
    }
}