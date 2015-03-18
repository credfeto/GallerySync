// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageMagickImageConverter.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Image converter that uses ImageMagick.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Globalization;
using System.IO;
using GraphicsMagick;
using OutputBuilderClient.Properties;

#endregion

namespace OutputBuilderClient.ImageConverters
{
    /// <summary>
    ///     Image converter that uses ImageMagick.
    /// </summary>
    [SupportedExtension("psd")]
    [SupportedExtension("tga")]
    internal class ImageMagickImageConverter : IImageConverter
    {
        #region Properties

        /// <summary>
        ///     Gets the name of the image magick convert executable.
        /// </summary>
        /// <value>
        ///     The name of the image magick convert executable.
        /// </value>
        private static string ImageMagickConvertExecutableName
        {
            get { return Settings.Default.ImageMagickConvertExecutable; }
        }

        #endregion

        #region Implemented Interfaces

        #region IImageConverter

        public Bitmap LoadImage(string fileName)
        {
            var bytes = LoadImageAsBytest(fileName);
            using (var stream = new MemoryStream(bytes, false))
            {
                return new Bitmap(stream);
            }
        }

        private static byte[] LoadImageAsBytest(string fileName)
        {
            using (MagickImage image = new MagickImage(fileName))
            {
                // Sets the output format to jpeg
                image.Format = MagickFormat.Jpeg;
                image.Quality = 100;

                return image.ToByteArray();
            }
        }

        /// <summary>
        ///     Loads the image.
        /// </summary>
        /// <param name="fileName">
        ///     The filename.
        /// </param>
        /// <returns>
        ///     An image, if it could be loaded.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Handling failure of the create process")]
        public Bitmap LoadImageOld(string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            string imageMagick = ImageMagickConvertExecutableName;
            if (string.IsNullOrEmpty(imageMagick))
            {
                return null;
            }

            using (new TemporaryFilesCleaner("*magick*.*"))
            {
                using (Process process = CreateProcessHighQuality(imageMagick, fileName))
                {
                    if (!process.Start())
                    {
                        Debug.WriteLine(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Executing : {0} {1}",
                                process.StartInfo.FileName,
                                process.StartInfo.Arguments));
                        return null;
                    }

                    using (Stream stream = process.StandardOutput.BaseStream)
                    {
                        try
                        {
                            Bitmap bmp = OpenBitmapFromStream(stream, fileName);
                            try
                            {
                                process.WaitForExit();

                                return bmp;
                            }
                            catch (Win32Exception)
                            {
                                Console.WriteLine(
                                    "ImageMagick: Convert image {0} to TIFF", fileName);
                            }
                            catch (SystemException)
                            {
                                Console.WriteLine(
                                    "ImageMagick: Convert image {0} to TIFF", fileName);
                            }

                            if (bmp != null)
                            {
                                bmp.Dispose();
                            }
                        }
                        catch (OutOfMemoryException)
                        {
                            Console.WriteLine(
                                "ImageMagick: Convert image {0} to TIFF", fileName);

                            // Force a collection
                            GC.GetTotalMemory(true);

                            return LoadImageLowQuality(fileName);
                        }
                    }

                    return null;
                }
            }
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>
        ///     Creates the high quality image processing utility.
        /// </summary>
        /// <param name="imageMagick">
        ///     The image magick executable.
        /// </param>
        /// <param name="fileName">
        ///     Name of the file.
        /// </param>
        /// <returns>
        ///     The process definition to create a high quality image.
        /// </returns>
        private static Process CreateProcessHighQuality(string imageMagick, string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(imageMagick));
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            return new Process
                {
                    StartInfo =
                        {
                            FileName = imageMagick,
                            Arguments = string.Format(CultureInfo.InvariantCulture, "\"{0}\" tiff:-", fileName),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = false
                        }
                };
        }

        /// <summary>
        ///     Creates the low quality image processing utility.
        /// </summary>
        /// <param name="imageMagick">
        ///     The image magick executable.
        /// </param>
        /// <param name="fileName">
        ///     Name of the file.
        /// </param>
        /// <returns>
        ///     The process definition to create a low quality image.
        /// </returns>
        private static Process CreateProcessLowQuality(string imageMagick, string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(imageMagick));
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            return new Process
                {
                    StartInfo =
                        {
                            FileName = imageMagick,
                            Arguments = string.Format(CultureInfo.InvariantCulture, "-depth 8 \"{0}\" tiff:-", fileName),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = false
                        }
                };
        }

        /// <summary>
        ///     Loads the image as a low quality image (when out of memory occurs).
        /// </summary>
        /// <param name="fileName">
        ///     Name of the file.
        /// </param>
        /// <returns>
        ///     The bitmap, if it was converted. Null otherwise.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Handling failure of the create process")]
        private static Bitmap LoadImageLowQuality(string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            string imageMagick = ImageMagickConvertExecutableName;
            if (string.IsNullOrEmpty(imageMagick))
            {
                return null;
            }

            using (Process process = CreateProcessLowQuality(imageMagick, fileName))
            {
                if (!process.Start())
                {
                    Debug.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Executing : {0} {1}",
                            process.StartInfo.FileName,
                            process.StartInfo.Arguments));
                    return null;
                }

                using (Stream stream = process.StandardOutput.BaseStream)
                {
                    try
                    {
                        Bitmap bmp = OpenBitmapFromStream(stream, fileName);
                        try
                        {
                            process.WaitForExit();

                            return bmp;
                        }
                        catch (Win32Exception)
                        {
                            Console.WriteLine(
                                "ImageMagick: Convert image {0} to TIFF (Low Quality)",
                                fileName);
                        }
                        catch (SystemException)
                        {
                            Console.WriteLine(
                                "ImageMagick: Convert image {0} to TIFF (Low Quality)",
                                fileName);
                        }

                        if (bmp != null)
                        {
                            bmp.Dispose();
                        }
                    }
                    catch (OutOfMemoryException)
                    {
                        Console.WriteLine(
                            "ImageMagick: Convert image {0} to TIFF (Low Quality)",
                            fileName);
                    }
                }

                return null;
            }
        }

        /// <summary>
        ///     Opens the bitmap from stream.
        /// </summary>
        /// <param name="stream">
        ///     The stream.
        /// </param>
        /// <param name="fileName">
        ///     The filename.
        /// </param>
        /// <returns>
        ///     An image that was contained in the stream.
        /// </returns>
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "fileName",
            Justification = "Handling failure of the create bitmap from string")]
        private static Bitmap OpenBitmapFromStream(Stream stream, string fileName)
        {
            Contract.Requires(stream != null);
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            try
            {
                return new Bitmap(stream);
            }
            catch
            {
                Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "Failed to open or convert {0}", fileName));
                throw;
            }
        }

        #endregion
    }
}