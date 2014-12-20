// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RAWImageConverter.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Image converter that uses DCRAW.
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
using OutputBuilderClient.Properties;

#endregion

namespace OutputBuilderClient.ImageConverters
{
    /// <summary>
    ///     Image converter that uses DCRAW.
    /// </summary>
    [SupportedExtension("arw")]
    [SupportedExtension("cf2")]
    [SupportedExtension("cr2")]
    [SupportedExtension("crw")]
    [SupportedExtension("dng")]
    [SupportedExtension("erf")]
    [SupportedExtension("mef")]
    [SupportedExtension("mrw")]
    [SupportedExtension("nef")]
    [SupportedExtension("orf")]
    [SupportedExtension("pef")]
    [SupportedExtension("raf")]
    [SupportedExtension("raw")]
    [SupportedExtension("rw2")]
    [SupportedExtension("sr2")]
    [SupportedExtension("x3f")]
    internal sealed class RawImageConverter : IImageConverter
    {
        #region Constants and Fields

        /// <summary>
        ///     The Tiff converter.
        /// </summary>
        private static readonly IImageConverter TiffConverter = new ImageMagickImageConverter();

        #endregion

        #region Properties

        /// <summary>
        ///     Gets the DCRaw executable.
        /// </summary>
        /// <value>
        ///     The DCRaw executable.
        /// </value>
        private static string DcRawExecutable
        {
            get { return Settings.Default.DCRAWExecutable; }
        }

        #endregion

        #region Implemented Interfaces

        #region IImageConverter

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
            Justification = "Calling external process which may cause crashes")]
        public Bitmap LoadImage(string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            var image = LoadImageInternal(fileName);
            if (image != null)
            {
                const int rotationDegrees = 0;

                ImageHelpers.RotateImageIfNecessary(image, rotationDegrees);
            }

            return image;
        }

        private static Bitmap LoadImageInternal(string fileName)
        {
            byte[] bytes = ConvertToTiffArray(fileName);
            if (bytes == null)
            {
                return null;
            }

            if (bytes.Length == 0)
            {
                return null;
            }

            try
            {
                Bitmap img = ConvertUsingGdiPlus(bytes, fileName);
                if (img != null)
                {
                    return img;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("DCRaw: Convert image {0} to TIFF", fileName);
            }

            return ConvertUsingImageMagick(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tif"), bytes);
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>
        ///     Converts to tiff array.
        /// </summary>
        /// <param name="filename">
        ///     The filename.
        /// </param>
        /// <returns>
        ///     The file as an array of bytes.
        /// </returns>
        private static byte[] ConvertToTiffArray(string filename)
        {
            Contract.Requires(!string.IsNullOrEmpty(filename));

            string dcraw = DcRawExecutable;
            if (string.IsNullOrEmpty(dcraw))
            {
                return null;
            }

            using (Process process = CreateProcess(dcraw, filename))
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
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);

                        process.WaitForExit();

                        return memoryStream.ToArray();
                    }
                }
            }
        }

        /// <summary>
        ///     Converts the block of data to a Bitmap.
        /// </summary>
        /// <param name="data">
        ///     The data to convert to a jpeg.
        /// </param>
        /// <param name="fileName">
        ///     The file name.
        /// </param>
        /// <returns>
        ///     The converted image.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Catching broken images.")]
        private static Bitmap ConvertUsingGdiPlus(byte[] data, string fileName)
        {
            Contract.Requires(data != null);
            Contract.Requires(data.Length > 0);
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            using (var stream = new MemoryStream(data, false))
            {
                try
                {
                    Bitmap bmp = OpenBitmapFromStream(stream, fileName);
                    try
                    {
                        return bmp;
                    }
                    catch (Win32Exception)
                    {
                        Console.WriteLine("DCRaw: Convert image {0} to TIFF", fileName);
                    }
                    catch (SystemException)
                    {
                        Console.WriteLine("DCRaw: Convert image {0} to TIFF", fileName);
                    }

                    bmp.Dispose();
                }
                catch (OutOfMemoryException)
                {
                    Console.WriteLine("DCRaw: Convert image {0} to TIFF", fileName);
                }

                return null;
            }
        }

        /// <summary>
        ///     Converts the bytes using image magick.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="bytes">The TIFF bytes.</param>
        /// <returns>
        ///     The converted bitmap.
        /// </returns>
        private static Bitmap ConvertUsingImageMagick(string fileName, byte[] bytes)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));
            Contract.Requires(bytes != null);
            Contract.Requires(bytes.Length > 0);

            try
            {
                File.WriteAllBytes(fileName, bytes);

                return TiffConverter.LoadImage(fileName);
            }
            finally
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        /// <summary>
        ///     Creates the process.
        /// </summary>
        /// <param name="dcraw">
        ///     The filename of the DCRAW executable.
        /// </param>
        /// <param name="fileName">
        ///     Name of the file.
        /// </param>
        /// <returns>
        ///     The process.
        /// </returns>
        private static Process CreateProcess(string dcraw, string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(dcraw));
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            return new Process
                {
                    StartInfo =
                        {
                            FileName = dcraw,
                            Arguments = string.Format(CultureInfo.InvariantCulture, "-w -q 3 -c -T \"{0}\"", fileName),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                };
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
        ///     The image that was contained in the stream.
        /// </returns>
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "fileName",
            Justification = "Used for logging")]
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