﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FileNaming;
using GraphicsMagick;
using OutputBuilderClient.Properties;
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

        public static List<ImageSize> BuildImages(Photo sourcePhoto, List<string> filesCreated, DateTime creationDate)
        {
            var sizes = new List<ImageSize>();

            string rawExtension = sourcePhoto.ImageExtension.TrimStart('.').ToUpperInvariant();

            IImageConverter converter;
            if (RegisteredConverters.TryGetValue(rawExtension, out converter))
            {
                int[] imageSizes =
                    StandardImageSizesWithThumbnailSize();

                string filename = Path.Combine(Settings.Default.RootFolder,
                                               sourcePhoto.BasePath + sourcePhoto.ImageExtension);

                using (MagickImage sourceBitmap = converter.LoadImage(filename))
                {
                    int sourceImageWidth = sourceBitmap.Width;
                    int sourceImageHeight = sourceBitmap.Height;

                    foreach (
                        int dimension in
                            imageSizes.Where(
                                size => ResziedImageWillNotBeBigger(size, sourceImageWidth, sourceImageHeight)))
                    {
                        using (MagickImage resized = ResizeImage(sourceBitmap, dimension))
                        {
                            ApplyWatermark(resized);
                            int quality =
                                Settings.Default.JpegOutputQuality;
                            byte[] resizedBytes = SaveImageAsJpegBytes(resized, quality);

                            if (!ImageHelpers.IsValidJpegImage(resizedBytes))
                            {
                                throw new Exception(string.Format("File {0} produced an invalid image", filename));
                            }

                            string resizedFileName = Path.Combine(Settings.Default.ImagesOutputPath,
                                                                  HashNaming.PathifyHash(sourcePhoto.PathHash),
                                                                  IndividualResizeFileName(sourcePhoto, resized));

                            WriteImage(resizedFileName, resizedBytes, creationDate);

                            filesCreated.Add(HashNaming.PathifyHash(sourcePhoto.PathHash) + "\\" +
                                             IndividualResizeFileName(sourcePhoto, resized));

                            if (resized.Width == Settings.Default.ThumbnailSize)
                            {
                                resizedFileName = Path.Combine(Settings.Default.ImagesOutputPath,
                                                               HashNaming.PathifyHash(sourcePhoto.PathHash),
                                                               IndividualResizeFileName(sourcePhoto, resized, "png"));
                                resizedBytes = SaveImageAsPng(resized);
                                WriteImage(resizedFileName, resizedBytes, creationDate);

                                filesCreated.Add(HashNaming.PathifyHash(sourcePhoto.PathHash) + "\\" +
                                                 IndividualResizeFileName(sourcePhoto, resized, "png"));
                            }
                            sizes.Add(new ImageSize
                                {
                                    Width = resized.Width,
                                    Height = resized.Height
                                });
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

        private static byte[] SaveImageAsPng(MagickImage image)
        {
            Contract.Requires(image != null);
            Contract.Ensures(Contract.Result<byte[]>() != null);

            StripExifProperties(image);
            SetCopyrightExifProperties(image);

            var qSettings = new QuantizeSettings { Colors = 256, Dither = true, ColorSpace = ColorSpace.RGB};
            image.Quantize(qSettings);

            return image.ToByteArray(MagickFormat.Png8);
        }

        private static bool ResziedImageWillNotBeBigger(int size, int sourceImageWidth, int sourceImageHeight)
        {
            // || size <= sourceImageHeight

            return size <= sourceImageWidth;
        }

        private static int[] StandardImageSizesWithThumbnailSize()
        {
            return Settings.Default.ImageMaximumDimensions.Split(',')
                           .Select(value => Convert.ToInt32(value))
                           .Concat(new[] {Settings.Default.ThumbnailSize})
                           .OrderByDescending(x => x)
                           .ToArray();
        }

        public static string IndividualResizeFileName(Photo sourcePhoto, MagickImage resized)
        {
            return IndividualResizeFileName(sourcePhoto, resized, "jpg");
        }

        public static string IndividualResizeFileName(Photo sourcePhoto, MagickImage resized, string extension)
        {
            string basePath = UrlNaming.BuildUrlSafePath(
                string.Format("{0}-{1}x{2}",
                              Path.GetFileName(
                                  sourcePhoto.BasePath),
                              resized.Width, resized.Height)).TrimEnd('/').TrimStart('-');

            return basePath + "." + extension;
        }


        public static string IndividualResizeFileName(Photo sourcePhoto, ImageSize resized)
        {
            return IndividualResizeFileName(sourcePhoto, resized, "jpg");
        }

        public static string IndividualResizeFileName(Photo sourcePhoto, ImageSize resized, string extension)
        {
            string basePath = UrlNaming.BuildUrlSafePath(
                string.Format("{0}-{1}x{2}",
                              Path.GetFileName(
                                  sourcePhoto.BasePath),
                              resized.Width, resized.Height)).TrimEnd('/').TrimStart('-');

            return basePath + "." + extension;
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
            Contract.Requires((int) (((double) maximumDimension/(double) image.Width)*(double) image.Height) > 0);
            Contract.Ensures(Contract.Result<MagickImage>() != null);

            int yscale = CalculateScaledHeightFromWidth(maximumDimension, image.Width, image.Height);

            MagickImage resized = null;
            try
            {
                resized = image.Clone();

                var geometry = new MagickGeometry(maximumDimension, yscale)
                    {
                        IgnoreAspectRatio = true
                    };

                resized.Resize(geometry);

                Debug.Assert(resized.Width == maximumDimension);
                Debug.Assert(resized.Height == yscale);

                return resized;
            }
            catch
            {
                if (resized != null)
                {
                    resized.Dispose();
                }

                throw;
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
        private static void ApplyWatermark(MagickImage imageToAddWatermarkTo)
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

            using (var watermark = new MagickImage())
            {
                //watermark.Warning += (sender, e) =>
                //{
                //    Console.WriteLine("Watermark Image Load Error: {0}", e.Message);
                //    throw e.Exception;
                //};

                watermark.BackgroundColor = MagickColor.Transparent;
                watermark.Read(watermarkFilename);

                if ((imageToAddWatermarkTo.Width <= watermark.Width) || (imageToAddWatermarkTo.Height <= watermark.Height))
                {
                    return;
                }

                int x = imageToAddWatermarkTo.Width - watermark.Width;
                int y = imageToAddWatermarkTo.Height - watermark.Height;

                watermark.BackgroundColor = MagickColor.Transparent;

                imageToAddWatermarkTo.Composite(watermark, x, y, CompositeOperator.Over);
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
        public static byte[] SaveImageAsJpegBytes(MagickImage image, long compressionQuality)
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

        /// <summary>
        ///     Sets the copyright EXIF properties.
        /// </summary>
        /// <param name="image">
        ///     The image to set the properties to it.
        /// </param>
        private static void SetCopyrightExifProperties(MagickImage image)
        {
            Contract.Requires(image != null);

            string copyright = CopyrightDeclaration;
            const string credit = "Camera owner, Mark Ridgwell; Photographer, Mark Ridgwell; Image creator, Mark Ridgwell";
            const string licensing = "For licensing information see https://www.markridgwell.co.uk/about";
            const string program = "https://www.markridgwell.co.uk/";

            Action<ExifProfile> completeExifProfile = (p) => { };
            Action<IptcProfile> completeIptcProfile = (p) => { };

            ExifProfile exifProfile = image.GetExifProfile();
            if (exifProfile == null)
            {
                exifProfile = new ExifProfile();

                completeExifProfile = image.AddProfile;                
            }


            exifProfile.SetValue(ExifTag.Copyright, copyright);

            exifProfile.SetValue(ExifTag.UserComment,
                                 Encoding.UTF8.GetBytes(
                                     licensing));

            exifProfile.SetValue(ExifTag.Artist,
                                 credit);

            exifProfile.SetValue(ExifTag.ImageDescription, program);


            IptcProfile iptcProfile = image.GetIptcProfile();
            if (iptcProfile == null)
            {
                iptcProfile = new IptcProfile();

                completeIptcProfile = image.AddProfile;
            }

            iptcProfile.SetValue(IptcTag.CopyrightNotice, CopyrightDeclaration);

            iptcProfile.SetValue(IptcTag.Caption,
                                 licensing);

            iptcProfile.SetValue(IptcTag.Credit,
                                 credit);

            iptcProfile.SetValue(IptcTag.OriginatingProgram, program);

            completeExifProfile(exifProfile);
            completeIptcProfile(iptcProfile);
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

            IptcProfile ipctProfile = image.GetIptcProfile();
            if (ipctProfile != null)
            {
                image.RemoveProfile(ipctProfile.Name);
            }

            ExifProfile exifProfile = image.GetExifProfile();
            if (exifProfile != null)
            {
                image.RemoveProfile(exifProfile.Name);
            }

            XmpProfile xmpProfile = image.GetXmpProfile();
            if (xmpProfile != null)
            {
                image.RemoveProfile(xmpProfile.Name);
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
        public static void WriteImage(string fileName, byte[] data, DateTime creationDate)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));
            Contract.Requires(data != null);

            EnsureFolderExistsForFile(fileName);

            FileHelpers.WriteAllBytes(fileName, data);

            SetCreationDate(fileName, creationDate);
        }


        private static void EnsureFolderExistsForFile(string fileName)
        {
            string folder = Path.GetDirectoryName(fileName);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        private static void SetCreationDate(string fileName, DateTime creationDate)
        {
            if (creationDate != DateTime.MinValue && File.Exists(fileName))
            {
                File.SetCreationTimeUtc(fileName, creationDate);
                File.SetLastWriteTimeUtc(fileName, creationDate);
                File.SetLastAccessTimeUtc(fileName, creationDate);
            }
        }
    }
}