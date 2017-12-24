// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RAWImageConverter.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Image converter that uses DCRAW.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Threading.Tasks;
using GraphicsMagick;
using OutputBuilderClient.Properties;
using StorageHelpers;

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
        /// <summary>
        ///     The Tiff converter.
        /// </summary>
        private static readonly IImageConverter TiffConverter = new ImageMagickImageConverter();

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
        public MagickImage LoadImage(string fileName)
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

        /// <summary>
        ///     Converts the bytes using image magick.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="bytes">The TIFF bytes.</param>
        /// <returns>
        ///     The converted bitmap.
        /// </returns>
        private static async Task<MagickImage> ConvertUsingImageMagick(string fileName, byte[] bytes)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));
            Contract.Requires(bytes != null);
            Contract.Requires(bytes.Length > 0);

            try
            {
                await FileHelpers.WriteAllBytes(fileName, bytes);

                return TiffConverter.LoadImage(fileName);
            }
            finally
            {
                FileHelpers.DeleteFile(fileName);
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

            //string.Format(CultureInfo.InvariantCulture, "-6 -w -q 3 -c -T \"{0}\"", fileName),
            return new Process
            {
                StartInfo =
                {
                    FileName = dcraw,
                    Arguments =
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "-6 -w -q 3 -c -T \"{0}\"",
                            fileName),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
        }

        /// <summary>
        ///     Converts to tiff array.
        /// </summary>
        /// <param name="filename">
        ///     The filename.
        /// </param>
        /// <returns>
        ///     The file as an array of bytes.
        /// </returns>
        private static MagickImage LoadImageInternal(string filename)
        {
            Contract.Requires(!string.IsNullOrEmpty(filename));

            var dcraw = DcRawExecutable;
            if (string.IsNullOrEmpty(dcraw))
                return null;

            using (var process = CreateProcess(dcraw, filename))
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

                using (var stream = process.StandardOutput.BaseStream)
                {
                    MagickImage image = null;

                    try
                    {
                        image = ConverterCommon.OpenBitmapFromStream(stream);

                        process.WaitForExit();

                        return image;
                    }
                    catch (Exception)
                    {
                        if (image != null)
                            image.Dispose();

                        throw;
                    }
                }
            }
        }
    }
}