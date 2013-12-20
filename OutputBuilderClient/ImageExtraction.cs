﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FileNaming;
using OutputBuilderClient.Properties;
using Twaddle.Gallery.ObjectModel;
using Encoder = System.Drawing.Imaging.Encoder;

namespace OutputBuilderClient
{
    internal static class ImageExtraction
    {
        #region Constants and Fields

        /// <summary>
        ///     The Exif Authors Id.
        /// </summary>
        private const int ExifAuthorsId = 0x13B;

        /// <summary>
        ///     The Exif Copyright Id.
        /// </summary>
        private const int ExifCopyrightId = 0x8298;

        /// <summary>
        ///     The Exif Program Name Id.
        /// </summary>
        private const int ExifProgramNameId = 0x131;

        /// <summary>
        ///     The Exif User comment Id.
        /// </summary>
        private const int ExifUserCommentId = 0x9286;

        #endregion

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

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Copyright (c) 1998 - {0} Mark Ridgwell (http://www.markridgwell.co.uk/about)\0",
                    DateTime.UtcNow.Year);
            }
        }

        public static List<ImageSize> BuildImages(Photo sourcePhoto)
        {
            var sizes = new List<ImageSize>();

            string rawExtension = sourcePhoto.ImageExtension.TrimStart('.').ToUpperInvariant();

            IImageConverter converter;
            if (RegisteredConverters.TryGetValue(rawExtension, out converter))
            {
                int[] imageSizes =
                    Settings.Default.ImageMaximumDimensions.Split(',')
                            .Select(value => Convert.ToInt32(value))
                            .Concat(new[] {Settings.Default.ThumbnailSize})
                            .OrderByDescending(x => x)
                            .ToArray();

                string filename = Path.Combine(Settings.Default.RootFolder,
                                               sourcePhoto.BasePath + sourcePhoto.ImageExtension);

                using (Bitmap sourceBitmap = converter.LoadImage(filename))
                {
                    foreach (int dimension in imageSizes)
                    {
                        using (Image resized = ResizeImage(sourceBitmap, dimension))
                        {
                            ApplyWatermark(resized);
                            byte[] resizedBytes = SaveImageAsJpegBytes(resized, Settings.Default.JpegOutputQuality);

                            string resizedFileName = Path.Combine(Settings.Default.ImagesOutputPath,
                                                                  HashNaming.PathifyHash(sourcePhoto.PathHash),
                                                                  IndividualResizeFileName(sourcePhoto, resized));

                            WriteImage(resizedFileName, resizedBytes);
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("No image converter for {0}", rawExtension);
            }

            return sizes;
        }

        private static string IndividualResizeFileName(Photo sourcePhoto, Image resized)
        {
            return UrlNaming.BuildUrlSafePath(
                string.Format("{0}-{1}x{2}",
                              Path.GetFileName(
                                  sourcePhoto.BasePath),
                              resized.Width, resized.Height)) + ".jpg";
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
        private static Image ResizeImage(Image image, int maximumDimension)
        {
            Contract.Requires(image != null);
            Contract.Requires(image.Width > 0);
            Contract.Requires(image.Height > 0);
            Contract.Requires(maximumDimension > 0);
            Contract.Requires((int) (((double) maximumDimension/(double) image.Width)*(double) image.Height) > 0);
            Contract.Ensures(Contract.Result<Image>() != null);

            int yscale = CalculateScaledHeightFromWidth(maximumDimension, image.Width, image.Height);

            return new Bitmap(image, maximumDimension, yscale);
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
            Contract.Requires((int) (((double) scaleWidth/(double) originalWidth)*(double) originalHeight) > 0);
            Contract.Ensures(Contract.Result<int>() > 0);

            return (int) ((scaleWidth/(double) originalWidth)*originalHeight);
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

            Type[] interfaces = classType.GetInterfaces();
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

            Assembly ass = typeof (IImageConverter).Assembly;

            Type[] types = ass.GetTypes();
            foreach (Type t in types.Where(t => ImplementsInterface(t, typeof (IImageConverter))))
            {
                IImageConverter converter = null;
                object[] attributes = t.GetCustomAttributes(false);
                foreach (
                    SupportedExtensionAttribute supportedExtension in attributes.OfType<SupportedExtensionAttribute>())
                {
                    if (converter == null)
                    {
                        converter = (IImageConverter) Activator.CreateInstance(t);
                    }

                    converters.Add(supportedExtension.Extension.ToUpperInvariant(), converter);
                }
            }

            if (!converters.Any())
            {
                throw new ConfigurationErrorsException("No registered converters!");
            }

            return converters;
        }

        /// <summary>
        ///     Applies the watermark to the image.
        /// </summary>
        /// <param name="imageToAddWatermarkTo">
        ///     The image to add the watermark to.
        /// </param>
        private static void ApplyWatermark(Image imageToAddWatermarkTo)
        {
            Contract.Requires(imageToAddWatermarkTo != null);

            string watermarkFilename = Settings.Default.WatermarkImage;
            if (string.IsNullOrEmpty(watermarkFilename))
            {
                return;
            }

            if (!File.Exists(watermarkFilename))
            {
                return;
            }

            using (var image = new Bitmap(watermarkFilename))
            {
                if ((imageToAddWatermarkTo.Width <= image.Width) || (imageToAddWatermarkTo.Height <= image.Height))
                {
                    return;
                }

                using (Graphics g = Graphics.FromImage(imageToAddWatermarkTo))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    var pos = new Rectangle(
                        imageToAddWatermarkTo.Width - image.Width,
                        imageToAddWatermarkTo.Height - image.Height,
                        image.Width,
                        image.Height);

                    g.DrawImage(image, pos);
                }
            }
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
        /// <returns>
        ///     Block of bytes representing the image.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Is fallback position where it retries.")]
        public static byte[] SaveImageAsJpegBytes(Image image, long compressionQuality)
        {
            Contract.Requires(image != null);
            Contract.Requires(compressionQuality > 0);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            StripExifProperties(image);
            SetCopyrightExifProperties(image);

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
        private static byte[] SaveImageAsJpegBytesWithOptions(Image image, long compression)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            ImageCodecInfo codecInfo = GetEncoderInfo("image/jpeg");
            if (codecInfo == null)
            {
                return SaveImageAsJpegBytesWithoutOptions(image);
            }

            // Set the quality (n.b. must be a long)
            var ratio = new EncoderParameter(Encoder.Quality, compression);

            // Add the quality parameter to the list
            var codecParams = new EncoderParameters(1);
            codecParams.Param[0] = ratio;

            using (var ms = new MemoryStream())
            {
                image.Save(ms, codecInfo, codecParams);

                ms.Flush();

                return ms.ToArray();
            }
        }

        /// <summary>
        ///     Gets the encoder info.
        /// </summary>
        /// <param name="mimeType">
        ///     The mime type.
        /// </param>
        /// <returns>
        ///     The image CODEC.
        /// </returns>
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            Contract.Requires(!string.IsNullOrEmpty(mimeType));

            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();

            return encoders.FirstOrDefault(encoder => encoder.MimeType == mimeType);
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
        private static byte[] SaveImageAsJpegBytesWithoutOptions(Image image)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Jpeg);

                ms.Flush();

                return ms.ToArray();
            }
        }

        /// <summary>
        ///     Sets the copyright EXIF properties.
        /// </summary>
        /// <param name="image">
        ///     The image to set the properties to it.
        /// </param>
        private static void SetCopyrightExifProperties(Image image)
        {
            Contract.Requires(image != null);

            AddOrSetPropertyItem(image, ExifCopyrightId, CopyrightDeclaration);

            AddOrSetPropertyItem(
                image, ExifUserCommentId, "For licensing information see http://www.markridgwell.co.uk/about");

            AddOrSetPropertyItem(
                image,
                ExifAuthorsId,
                "Camera owner, Mark Ridgwell; Photographer, Mark Ridgwell; Image creator, Mark Ridgwell");

            AddOrSetPropertyItem(image, ExifProgramNameId, "http://www.markridgwell.co.uk/");
        }

        /// <summary>
        ///     Strips the EXIF properties from the image.
        /// </summary>
        /// <param name="image">
        ///     The image.
        /// </param>
        private static void StripExifProperties(Image image)
        {
            Contract.Requires(image != null);

            foreach (int propertyItemId in (from record in image.PropertyItems select record.Id).Distinct())
            {
                image.RemovePropertyItem(propertyItemId);
            }
        }

        /// <summary>
        ///     Adds or sets an Exif property.
        /// </summary>
        /// <param name="image">
        ///     The image.
        /// </param>
        /// <param name="item">
        ///     The EXIF id.
        /// </param>
        /// <param name="value">
        ///     The value.
        /// </param>
        private static void AddOrSetPropertyItem(Image image, int item, string value)
        {
            Contract.Requires(image != null);
            Contract.Requires(!string.IsNullOrEmpty(value));

            PropertyItem pi = CreateNewPropertyItemUsingWatermarkAsFallback(image, item);
            if (pi == null)
            {
                return;
            }

            byte[] bytes = Encoding.ASCII.GetBytes(value + "\0");

            pi.Id = item;
            pi.Type = 2;
            pi.Value = bytes;
            pi.Len = bytes.Length;

            image.SetPropertyItem(pi);
        }

        /// <summary>
        ///     Creates the new property item by cloning an existing one.
        /// </summary>
        /// <param name="image">
        ///     The image to create the property item in.
        /// </param>
        /// <param name="item">
        ///     The item to create.
        /// </param>
        /// <returns>
        ///     A Property item.
        /// </returns>
        private static PropertyItem CreateNewPropertyItem(Image image, int item)
        {
            Contract.Requires(image != null);

            if (!image.PropertyItems.Any())
            {
                return null;
            }

            IEnumerable<PropertyItem> pi = from propertyItem in image.PropertyItems
                                           where propertyItem.Id == item
                                           select propertyItem;

            PropertyItem piActual = pi.SingleOrDefault();

            return piActual ?? image.PropertyItems.First();
        }

        /// <summary>
        ///     Creates the new property item using watermark image as fallback for property item source.
        /// </summary>
        /// <param name="image">
        ///     The image to create the property item for.
        /// </param>
        /// <param name="item">
        ///     The item id to create.
        /// </param>
        /// <returns>
        ///     A Property item if it can be created in any way.
        /// </returns>
        private static PropertyItem CreateNewPropertyItemUsingWatermarkAsFallback(Image image, int item)
        {
            Contract.Requires(image != null);

            PropertyItem pi = CreateNewPropertyItem(image, item);
            if (pi == null)
            {
                string fileName = Settings.Default.WatermarkImage;
                if (string.IsNullOrEmpty(fileName))
                {
                    return pi;
                }

                if (File.Exists(fileName))
                {
                    using (Image srcImage = Image.FromFile(fileName))
                    {
                        pi = CreateNewPropertyItem(srcImage, item);
                    }
                }
            }

            return pi;
        }

        /// <summary>
        ///     Determines whether the data block is valid JPEG image.
        /// </summary>
        /// <param name="data">
        ///     The data in the file.
        /// </param>
        /// <returns>
        ///     <c>true</c>
        ///     if it is valid JPEG image; otherwise,
        ///     <c>false</c>
        ///     .
        /// </returns>
        private static bool IsValidJpegImage(byte[] data)
        {
            Contract.Requires(data != null);

            if (data.Length < 2)
            {
                return false;
            }

            // check for the existence of the EOI segment header at the end of the file
            // http://stackoverflow.com/questions/198438/efficiently-detect-corrupted-jpeg-file
            return (data[data.Length - 2] == 0xff) && (data[data.Length - 1] == 0xd9);
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
        public static void WriteImage(string fileName, byte[] data)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));
            Contract.Requires(data != null);

            if (!IsValidJpegImage(data))
            {
                Console.WriteLine("WriteImage: Invalid JPEG Image : {0}", fileName);
                return;
            }

            string folder = Path.GetDirectoryName(fileName);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            try
            {
                File.WriteAllBytes(fileName, data);
            }
            catch (Exception)
            {
                DeleteFile(fileName);
            }
        }

        private static void DeleteFile(string fileName)
        {
            try
            {
                File.Delete(fileName);
            }
            catch
            {
            }
        }
    }
}