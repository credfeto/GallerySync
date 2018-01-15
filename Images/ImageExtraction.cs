﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileNaming;
using ObjectModel;
using OutputBuilderClient;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Quantizers;
using StorageHelpers;

namespace Images
{
    public static class ImageExtraction
    {
        private static readonly Dictionary<string, IImageConverter> RegisteredConverters = LocateConverters();

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

        public static async Task<List<ImageSize>> BuildImages(
            Photo sourcePhoto,
            List<string> filesCreated,
            DateTime creationDate,
            string url,
            string shortUrl,
            ISettings settings)
        {
            var sizes = new List<ImageSize>();

            var rawExtension = sourcePhoto.ImageExtension.TrimStart('.').ToUpperInvariant();

            if (RegisteredConverters.TryGetValue(rawExtension, out var converter))
            {
                var imageSizes = StandardImageSizesWithThumbnailSize(settings);

                var filename = Path.Combine(
                    settings.RootFolder,
                    sourcePhoto.BasePath + sourcePhoto.ImageExtension);

                using (var sourceBitmap = converter.LoadImage(filename))
                {
                    var sourceImageWidth = sourceBitmap.Width;
                    var sourceImageHeight = sourceBitmap.Height;

                    foreach (var dimension in
                        imageSizes.Where(size => ResziedImageWillNotBeBigger(size, sourceImageWidth, sourceImageHeight))
                    )
                        using (var resized = ResizeImage(sourceBitmap, dimension))
                        {
                            var resizedFileName =
                                Path.Combine(
                                    settings.ImagesOutputPath,
                                    HashNaming.PathifyHash(sourcePhoto.PathHash),
                                    IndividualResizeFileName(sourcePhoto, resized));

                            ApplyWatermark(resized, shortUrl, settings);

                            var quality = settings.JpegOutputQuality;
                            var resizedBytes = SaveImageAsJpegBytes(
                                resized,
                                quality,
                                url,
                                shortUrl,
                                sourcePhoto.BasePath,
                                sourcePhoto.Metadata,
                                creationDate,
                                settings);

                            if (
                                !ImageHelpers.IsValidJpegImage(
                                    resizedBytes,
                                    "In memory image to be saved as: " + resizedFileName))
                                throw new Exception(string.Format("File {0} produced an invalid image", filename));

                            await WriteImage(resizedFileName, resizedBytes, creationDate);

                            if (
                                !ImageHelpers.IsValidJpegImage(
                                    File.ReadAllBytes(resizedFileName),
                                    "Saved resize image: " + resizedFileName))
                            {
                                Console.WriteLine("Error: File {0} produced an invalid image", resizedFileName);

                                throw new AbortProcessingException(
                                    string.Format("File {0} produced an invalid image", filename));
                            }

                            filesCreated.Add(
                                HashNaming.PathifyHash(sourcePhoto.PathHash) + "\\"
                                                                             + IndividualResizeFileName(sourcePhoto,
                                                                                 resized));

                            if (resized.Width == settings.ThumbnailSize)
                            {
                                resizedFileName =
                                    Path.Combine(
                                        settings.ImagesOutputPath,
                                        HashNaming.PathifyHash(sourcePhoto.PathHash),
                                        IndividualResizeFileName(sourcePhoto, resized, "png"));
                                resizedBytes = SaveImageAsPng(
                                    resized,
                                    url,
                                    shortUrl,
                                    sourcePhoto.BasePath,
                                    sourcePhoto.Metadata,
                                    creationDate,
                                    settings);
                                await WriteImage(resizedFileName, resizedBytes, creationDate);

                                filesCreated.Add(
                                    HashNaming.PathifyHash(sourcePhoto.PathHash) + "\\"
                                                                                 + IndividualResizeFileName(sourcePhoto,
                                                                                     resized, "png"));
                            }

                            sizes.Add(new ImageSize {Width = resized.Width, Height = resized.Height});
                        }
                }
            }
            else
            {
                Console.WriteLine("No image converter for {0}", rawExtension);
            }

            return sizes;
        }

        public static string IndividualResizeFileName(Photo sourcePhoto, Image<Rgba32> resized)
        {
            return IndividualResizeFileName(sourcePhoto, resized, "jpg");
        }

        public static string IndividualResizeFileName(Photo sourcePhoto, Image<Rgba32> resized, string extension)
        {
            var basePath =
                UrlNaming.BuildUrlSafePath(
                    string.Format(
                        "{0}-{1}x{2}",
                        Path.GetFileName(sourcePhoto.BasePath),
                        resized.Width,
                        resized.Height)).TrimEnd('/').TrimStart('-');

            return basePath + "." + extension;
        }

        public static string IndividualResizeFileName(Photo sourcePhoto, ImageSize resized)
        {
            return IndividualResizeFileName(sourcePhoto, resized, "jpg");
        }

        public static string IndividualResizeFileName(Photo sourcePhoto, ImageSize resized, string extension)
        {
            var basePath =
                UrlNaming.BuildUrlSafePath(
                    string.Format(
                        "{0}-{1}x{2}",
                        Path.GetFileName(sourcePhoto.BasePath),
                        resized.Width,
                        resized.Height)).TrimEnd('/').TrimStart('-');

            return basePath + "." + extension;
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
        /// <param name="filePath"></param>
        /// <param name="metadata"></param>
        /// <param name="creationDate"></param>
        /// <returns>
        ///     Block of bytes representing the image.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Is fallback position where it retries.")]
        public static byte[] SaveImageAsJpegBytes(
            Image<Rgba32> image,
            long compressionQuality,
            string url,
            string shortUrl,
            string filePath,
            List<PhotoMetadata> metadata,
            DateTime creationDate,
            ISettings settings)
        {
            Contract.Requires(image != null);
            Contract.Requires(compressionQuality > 0);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            SetMetadataProperties(image, url, shortUrl, filePath, metadata, creationDate, settings);

            try
            {
                return SaveImageAsJpegBytesWithOptions(image, compressionQuality);
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
        public static async Task WriteImage(string fileName, byte[] data, DateTime creationDate)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));
            Contract.Requires(data != null);

            EnsureFolderExistsForFile(fileName);

            await FileHelpers.WriteAllBytes(fileName, data);

            MetadataOutput.SetCreationDate(fileName, creationDate);
        }

        /// <summary>
        ///     Applies the watermark to the image.
        /// </summary>
        /// <param name="imageToAddWatermarkTo">
        ///     The image to add the watermark to.
        /// </param>
        /// <param name="url"></param>
        private static void ApplyWatermark(Image<Rgba32> imageToAddWatermarkTo, string url, ISettings settings)
        {
            Contract.Requires(imageToAddWatermarkTo != null);

            const int spacer = 5;

            var watermarkFilename = settings.WatermarkImage;
            if (string.IsNullOrEmpty(watermarkFilename))
                return;

            if (!File.Exists(watermarkFilename))
                return;

            using (var watermark = new MagickImage())
            {
                //watermark.Warning += (sender, e) =>
                //{
                //    Console.WriteLine("Watermark Image Load Error: {0}", e.Message);
                //    throw e.Exception;
                //};
                CompositeOperator compositionOperator;
                watermark.BackgroundColor = MagickColor.Transparent;
                watermark.Read(watermarkFilename);

                var width = watermark.Width;
                var height = watermark.Height;

                using (var qr = EncodeUrl(url, watermark.Height))
                {
                    if (qr != null)
                    {
                        qr.BackgroundColor = MagickColor.Transparent;

                        var qrWidth = qr.Width;
                        var qrHeight = qr.Height;

                        var qrXPos = imageToAddWatermarkTo.Width - (qrWidth + spacer);
                        var qrYpos = imageToAddWatermarkTo.Height - (qrHeight + spacer);

                        width = watermark.Width + (qrWidth > 0 ? qrWidth + 2 * spacer : 0);

                        var maxHeight = Math.Max(watermark.Height, qrHeight + spacer);

                        if (imageToAddWatermarkTo.Width <= width || imageToAddWatermarkTo.Height <= maxHeight)
                            return;

                        compositionOperator = CompositeOperator.Over;
                        imageToAddWatermarkTo.Composite(qr, qrXPos, qrYpos, compositionOperator);
                    }
                }

                if (imageToAddWatermarkTo.Width <= width || imageToAddWatermarkTo.Height <= height)
                    return;

                var x = imageToAddWatermarkTo.Width - width;
                var y = imageToAddWatermarkTo.Height - height;

                compositionOperator = CompositeOperator.Over;
                imageToAddWatermarkTo.Composite(watermark, x, y, compositionOperator);
            }
        }

        private static int CaclulateQrModuleSize(int height)
        {
            //var moduleSize = height/33;
            //if (height%33 != 0)
            //{
            //    moduleSize += 1;
            //}
            //return moduleSize;
            return 2;
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

        private static Image<Rgba32> EncodeUrl(string url, int height)
        {
            //url = "https://www.markridgwell.co.uk/";
            var encoder = new QrEncoder(ErrorCorrectionLevel.H);
            QrCode qr;
            if (encoder.TryEncode(url, out qr))
            {
                var moduleSize = CaclulateQrModuleSize(height);

                using (var stream = new MemoryStream())
                {
                    var darkBrush = Brushes.Black;
                    Brush lightBrush = new SolidBrush(Color.FromArgb(128, 255, 255, 255));

                    var renderer = new GraphicsRenderer(
                        new FixedModuleSize(moduleSize, QuietZoneModules.Two),
                        darkBrush,
                        lightBrush);

                    renderer.WriteToStream(qr.Matrix, ImageFormat.Png, stream);

                    return new MagickImage(stream.ToArray());
                }
            }

            return null;
        }

        private static void EnsureFolderExistsForFile(string fileName)
        {
            var folder = Path.GetDirectoryName(fileName);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private static string ExtractDescription(
            List<PhotoMetadata> metadata,
            string url,
            string shortUrl,
            DateTime creationDate,
            ISettings settings)
        {
            var description = string.Empty;
            var desc =
                metadata.FirstOrDefault(
                    item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Comment));
            if (desc != null)
                description = desc.Value;

            if (!string.IsNullOrWhiteSpace(description))
                description += ". ";

            description += "Source : ";
            if (StringComparer.InvariantCultureIgnoreCase.Equals(settings.DefaultShortUrl, shortUrl))
                description += url;
            else
                description += shortUrl;

            description += " Photo taken by Mark Ridgwell";
            if (creationDate != DateTime.MinValue)
                description += " (" + creationDate.ToString("yyyy-MM-dd") + ")";

            description += ".";

            return description;
        }

        private static string ExtractTitle(string path, List<PhotoMetadata> metadata)
        {
            var title = string.Empty;
            var desc =
                metadata.FirstOrDefault(
                    item => StringComparer.InvariantCultureIgnoreCase.Equals(item.Name, MetadataNames.Title));
            if (desc != null)
                title = desc.Value;

            if (string.IsNullOrWhiteSpace(title))
            {
                var fragments =
                    path.Split('\\').Where(candidate => !string.IsNullOrWhiteSpace(candidate)).ToArray();

                if (fragments.Length > 0)
                    title = fragments[fragments.Length - 1];
            }

            return title;
        }

        /// <summary>
        ///     Checks to see if the class implements the interface.
        /// </summary>
        /// <param name="classType">
        ///     Type of the class.
        /// </param>
        /// <param name="interfaceType">
        ///     Type of the interface.
        /// </param>
        /// <returns>
        ///     True, if the class implements the interface;false, otherwise.
        /// </returns>
        private static bool ImplementsInterface(Type classType, Type interfaceType)
        {
            Contract.Requires(classType != null);
            Contract.Requires(interfaceType != null);

            var interfaces = classType.GetInterfaces();
            return interfaces.Any(i => i == interfaceType);
        }

        /// <summary>
        ///     Locates the converters.
        /// </summary>
        /// <returns>
        ///     List of converters.
        /// </returns>
        /// <exception cref="ConfigurationErrorsException">No registered converters were loaded.</exception>
        private static Dictionary<string, IImageConverter> LocateConverters()
        {
            Contract.Ensures(Contract.Result<Dictionary<string, IImageConverter>>() != null);

            var converters = new Dictionary<string, IImageConverter>();

            var ass = typeof(IImageConverter).Assembly;

            var types = ass.GetTypes();
            foreach (var t in types.Where(t => ImplementsInterface(t, typeof(IImageConverter))))
            {
                IImageConverter converter = null;
                var attributes = t.GetCustomAttributes(false);
                foreach (
                    var supportedExtension in attributes.OfType<SupportedExtensionAttribute>())
                {
                    if (converter == null)
                        converter = (IImageConverter) Activator.CreateInstance(t);

                    converters.Add(supportedExtension.Extension.ToUpperInvariant(), converter);
                }
            }

            if (!converters.Any())
                throw new ConfigurationErrorsException("No registered converters!");

            return converters;
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

            var yscale = CalculateScaledHeightFromWidth(maximumDimension, image.Width, image.Height);

            Image<Rgba32> resized = null;
            try
            {
                resized = image.Clone(x => x.Resize(maximumDimension, yscale));

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

        private static bool ResziedImageWillNotBeBigger(int size, int sourceImageWidth, int sourceImageHeight)
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

            using (var ms = new MemoryStream())
            {
                var encoder = new JpegEncoder
                {
                    IgnoreMetadata = true,
                    Quality = (int) compression
                };

                image.SaveAsJpeg(ms, encoder);

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

            using (var ms = new MemoryStream())
            {
                var encoder = new JpegEncoder
                {
                    IgnoreMetadata = true,
                    Quality = 75
                };

                image.SaveAsJpeg(ms, encoder);

                return ms.ToArray();
            }

            //return image.ToByteArray(MagickFormat.Jpeg);
        }

        private static byte[] SaveImageAsPng(
            Image<Rgba32> image,
            string url,
            string shortUrl,
            string filePath,
            List<PhotoMetadata> metadata,
            DateTime creationDate,
            ISettings settings)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            SetMetadataProperties(image, url, shortUrl, filePath, metadata, creationDate, settings);

            using (var ms = new MemoryStream())
            {
                var encoder = new PngEncoder
                {
                    IgnoreMetadata = true,
                    PaletteSize = 256,
                    CompressionLevel = 9,
                    Quantizer = new OctreeQuantizer<Rgb24>(),
                    PngColorType = PngColorType.Palette
                };
                image.SaveAsPng(ms, encoder);

                return ms.ToArray();
            }


//            var qSettings = new QuantizeSettings {Colors = 256, Dither = true, ColorSpace = ColorSpace.RGB};
//            image.Quantize(qSettings);
//
//            
//            
//            return image.ToByteArray(MagickFormat.Png8);
        }

        private static void SetExifMetadata(
            Image<Rgba32> image,
            DateTime creationDate,
            string description,
            string copyright,
            string licensing,
            string credit,
            string program)
        {
            var exifProfile = image.MetaData.ExifProfile;
            if (exifProfile == null) return;

            MetadataOutput.SetCreationDate(creationDate, exifProfile);

            MetadataOutput.SetDescription(description, exifProfile);
            MetadataOutput.SetCopyright(exifProfile, copyright);

            MetadataOutput.SetLicensing(exifProfile, licensing);

            MetadataOutput.SetPhotographer(exifProfile, credit);

            MetadataOutput.SetProgram(exifProfile, program);
        }

        /// <summary>
        ///     Sets the copyright EXIF properties.
        /// </summary>
        /// <param name="image">
        ///     The image to set the properties to it.
        /// </param>
        /// <param name="url"></param>
        /// <param name="shortUrl"></param>
        /// <param name="filePath"></param>
        /// <param name="metadata"></param>
        /// <param name="creationDate"></param>
        private static void SetMetadataProperties(
            Image<Rgba32> image,
            string url,
            string shortUrl,
            string filePath,
            List<PhotoMetadata> metadata,
            DateTime creationDate,
            ISettings settings)
        {
            Contract.Requires(image != null);

            var copyright = CopyrightDeclaration;
            const string credit =
                "Camera owner, Mark Ridgwell; Photographer, Mark Ridgwell; Image creator, Mark Ridgwell";
            const string licensing = "For licensing information see https://www.markridgwell.co.uk/about/";
            const string program = "https://www.markridgwell.co.uk/";
            var title = ExtractTitle(filePath, metadata);
            var description = ExtractDescription(metadata, url, shortUrl, creationDate, settings);

            SetExifMetadata(image, creationDate, description, copyright, licensing, credit, program);
        }

        private static int[] StandardImageSizesWithThumbnailSize(ISettings settings)
        {
            return
                settings.ImageMaximumDimensions.Split(',')
                    .Select(value => Convert.ToInt32(value))
                    .Concat(new[] {settings.ThumbnailSize})
                    .OrderByDescending(x => x)
                    .ToArray();
        }
    }
}