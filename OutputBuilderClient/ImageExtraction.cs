using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileNaming;
using Gma.QrCodeNet.Encoding;
using Gma.QrCodeNet.Encoding.Windows.Render;
using GraphicsMagick;
using OutputBuilderClient.Properties;
using StorageHelpers;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    internal static class ImageExtraction
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
            string shortUrl)
        {
            var sizes = new List<ImageSize>();

            var rawExtension = sourcePhoto.ImageExtension.TrimStart('.').ToUpperInvariant();

            IImageConverter converter;
            if (RegisteredConverters.TryGetValue(rawExtension, out converter))
            {
                var imageSizes = StandardImageSizesWithThumbnailSize();

                var filename = Path.Combine(
                    Settings.Default.RootFolder,
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
                                    Settings.Default.ImagesOutputPath,
                                    HashNaming.PathifyHash(sourcePhoto.PathHash),
                                    IndividualResizeFileName(sourcePhoto, resized));

                            ApplyWatermark(resized, shortUrl);

                            var quality = Settings.Default.JpegOutputQuality;
                            var resizedBytes = SaveImageAsJpegBytes(
                                resized,
                                quality,
                                url,
                                shortUrl,
                                sourcePhoto.BasePath,
                                sourcePhoto.Metadata,
                                creationDate);

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
                                + IndividualResizeFileName(sourcePhoto, resized));

                            if (resized.Width == Settings.Default.ThumbnailSize)
                            {
                                resizedFileName =
                                    Path.Combine(
                                        Settings.Default.ImagesOutputPath,
                                        HashNaming.PathifyHash(sourcePhoto.PathHash),
                                        IndividualResizeFileName(sourcePhoto, resized, "png"));
                                resizedBytes = SaveImageAsPng(
                                    resized,
                                    url,
                                    shortUrl,
                                    sourcePhoto.BasePath,
                                    sourcePhoto.Metadata,
                                    creationDate);
                                await WriteImage(resizedFileName, resizedBytes, creationDate);

                                filesCreated.Add(
                                    HashNaming.PathifyHash(sourcePhoto.PathHash) + "\\"
                                    + IndividualResizeFileName(sourcePhoto, resized, "png"));
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

        public static string IndividualResizeFileName(Photo sourcePhoto, MagickImage resized)
        {
            return IndividualResizeFileName(sourcePhoto, resized, "jpg");
        }

        public static string IndividualResizeFileName(Photo sourcePhoto, MagickImage resized, string extension)
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
            MagickImage image,
            long compressionQuality,
            string url,
            string shortUrl,
            string filePath,
            List<PhotoMetadata> metadata,
            DateTime creationDate)
        {
            Contract.Requires(image != null);
            Contract.Requires(compressionQuality > 0);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            StripExifProperties(image);
            SetMetadataProperties(image, url, shortUrl, filePath, metadata, creationDate);

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
        private static void ApplyWatermark(MagickImage imageToAddWatermarkTo, string url)
        {
            Contract.Requires(imageToAddWatermarkTo != null);

            const int spacer = 5;

            var watermarkFilename = Settings.Default.WatermarkImage;
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

        private static MagickImage EncodeUrl(string url, int height)
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
            DateTime creationDate)
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
            if (StringComparer.InvariantCultureIgnoreCase.Equals(Constants.DefaultShortUrl, shortUrl))
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
        private static MagickImage ResizeImage(MagickImage image, int maximumDimension)
        {
            Contract.Requires(image != null);
            Contract.Requires(image.Width > 0);
            Contract.Requires(image.Height > 0);
            Contract.Requires(maximumDimension > 0);
            Contract.Requires((int) (maximumDimension / (double) image.Width * image.Height) > 0);
            Contract.Ensures(Contract.Result<MagickImage>() != null);

            var yscale = CalculateScaledHeightFromWidth(maximumDimension, image.Width, image.Height);

            MagickImage resized = null;
            try
            {
                resized = image.Clone();

                var geometry = new MagickGeometry(maximumDimension, yscale) {IgnoreAspectRatio = true};

                resized.Resize(geometry);

                Debug.Assert(resized.Width == maximumDimension);
                Debug.Assert(resized.Height == yscale);

                return resized;
            }
            catch
            {
                if (resized != null)
                    resized.Dispose();

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
        private static byte[] SaveImageAsJpegBytesWithOptions(MagickImage image, long compression)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            image.Quality = (int) compression;
            return image.ToByteArray(MagickFormat.Jpeg);
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
        private static byte[] SaveImageAsJpegBytesWithoutOptions(MagickImage image)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            return image.ToByteArray(MagickFormat.Jpeg);
        }

        private static byte[] SaveImageAsPng(
            MagickImage image,
            string url,
            string shortUrl,
            string filePath,
            List<PhotoMetadata> metadata,
            DateTime creationDate)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            StripExifProperties(image);
            SetMetadataProperties(image, url, shortUrl, filePath, metadata, creationDate);

            var qSettings = new QuantizeSettings {Colors = 256, Dither = true, ColorSpace = ColorSpace.RGB};
            image.Quantize(qSettings);

            return image.ToByteArray(MagickFormat.Png8);
        }

        private static void SetExifMetadata(
            MagickImage image,
            DateTime creationDate,
            string description,
            string copyright,
            string licensing,
            string credit,
            string program)
        {
            Action<ExifProfile> completeExifProfile = p => { };
            var exifProfile = image.GetExifProfile();
            if (exifProfile == null)
            {
                exifProfile = new ExifProfile();

                completeExifProfile = image.AddProfile;
            }

            MetadataOutput.SetCreationDate(creationDate, exifProfile);

            MetadataOutput.SetDescription(description, exifProfile);
            MetadataOutput.SetCopyright(exifProfile, copyright);

            MetadataOutput.SetLicensing(exifProfile, licensing);

            MetadataOutput.SetPhotographer(exifProfile, credit);

            MetadataOutput.SetProgram(exifProfile, program);

            completeExifProfile(exifProfile);
        }

        private static void SetIptcMetadata(
            MagickImage image,
            string url,
            DateTime creationDate,
            string title,
            string description,
            string credit,
            string program,
            string licensing)
        {
            Action<IptcProfile> completeIptcProfile = p => { };

            var iptcProfile = image.GetIptcProfile();
            if (iptcProfile == null)
            {
                iptcProfile = new IptcProfile();

                completeIptcProfile = image.AddProfile;
            }

            try
            {
                SetIptcMetadataInternal(iptcProfile, url, creationDate, title, description, credit, program, licensing);
            }
            catch
            {
                // Ok some images are crap and don't like the normal way of setting metadata
                iptcProfile = new IptcProfile();
                completeIptcProfile = p =>
                {
                    image.RemoveProfile(p.Name);
                    image.AddProfile(p);
                };

                SetIptcMetadataInternal(iptcProfile, url, creationDate, title, description, credit, program, licensing);
            }

            completeIptcProfile(iptcProfile);
        }

        private static void SetIptcMetadataInternal(
            IptcProfile iptcProfile,
            string url,
            DateTime creationDate,
            string title,
            string description,
            string credit,
            string program,
            string licensing)
        {
            MetadataOutput.SetTitle(title, iptcProfile);
            MetadataOutput.SetDescription(description, iptcProfile);
            MetadataOutput.SetCopyright(iptcProfile, CopyrightDeclaration);

            MetadataOutput.SetCredit(iptcProfile, credit);

            MetadataOutput.SetProgram(iptcProfile, program);

            MetadataOutput.SetTransmissionReference(url, iptcProfile);

            MetadataOutput.SetLicensing(iptcProfile, licensing);

            MetadataOutput.SetCreationDate(creationDate, iptcProfile);
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
            MagickImage image,
            string url,
            string shortUrl,
            string filePath,
            List<PhotoMetadata> metadata,
            DateTime creationDate)
        {
            Contract.Requires(image != null);

            var copyright = CopyrightDeclaration;
            const string credit =
                "Camera owner, Mark Ridgwell; Photographer, Mark Ridgwell; Image creator, Mark Ridgwell";
            const string licensing = "For licensing information see https://www.markridgwell.co.uk/about/";
            const string program = "https://www.markridgwell.co.uk/";
            var title = ExtractTitle(filePath, metadata);
            var description = ExtractDescription(metadata, url, shortUrl, creationDate);

            image.Format = MagickFormat.Jpeg;

            SetIptcMetadata(image, url, creationDate, title, description, credit, program, licensing);

            SetExifMetadata(image, creationDate, description, copyright, licensing, credit, program);
        }

        private static int[] StandardImageSizesWithThumbnailSize()
        {
            return
                Settings.Default.ImageMaximumDimensions.Split(',')
                    .Select(value => Convert.ToInt32(value))
                    .Concat(new[] {Settings.Default.ThumbnailSize})
                    .OrderByDescending(x => x)
                    .ToArray();
        }

        /// <summary>
        ///     Strips the EXIF properties from the image.
        /// </summary>
        /// <param name="image">
        ///     The image.
        /// </param>
        private static void StripExifProperties(MagickImage image)
        {
            Contract.Requires(image != null);

            var ipctProfile = image.GetIptcProfile();
            if (ipctProfile != null)
                image.RemoveProfile(ipctProfile.Name);

            var exifProfile = image.GetExifProfile();
            if (exifProfile != null)
                image.RemoveProfile(exifProfile.Name);

            var xmpProfile = image.GetXmpProfile();
            if (xmpProfile != null)
                image.RemoveProfile(xmpProfile.Name);
        }
    }
}