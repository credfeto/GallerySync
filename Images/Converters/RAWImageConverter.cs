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
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StorageHelpers;

namespace Images.Converters
{
    /// <summary>
    ///     Image converter that uses DCRAW.
    /// </summary>
    [SupportedExtension(extension: "arw")]
    [SupportedExtension(extension: "cf2")]
    [SupportedExtension(extension: "cr2")]
    [SupportedExtension(extension: "crw")]
    [SupportedExtension(extension: "dng")]
    [SupportedExtension(extension: "erf")]
    [SupportedExtension(extension: "mef")]
    [SupportedExtension(extension: "mrw")]
    [SupportedExtension(extension: "nef")]
    [SupportedExtension(extension: "orf")]
    [SupportedExtension(extension: "pef")]
    [SupportedExtension(extension: "raf")]
    [SupportedExtension(extension: "raw")]
    [SupportedExtension(extension: "rw2")]
    [SupportedExtension(extension: "sr2")]
    [SupportedExtension(extension: "x3f")]
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
        private static string DcRawExecutable => "C:\\utils\\imageprocessing\\dcraw.exe";

        /// <summary>
        ///     Loads the image.
        /// </summary>
        /// <param name="fileName">
        ///     The filename.
        /// </param>
        /// <returns>
        ///     An image, if it could be loaded.
        /// </returns>
        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Calling external process which may cause crashes")]
        public Image<Rgba32> LoadImage(string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            Image<Rgba32> image = LoadImageInternal(fileName);

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
        private static async Task<Image<Rgba32>> ConvertUsingImageMagick(string fileName, byte[] bytes)
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
                           Arguments = string.Format(CultureInfo.InvariantCulture, format: "-6 -w -q 3 -c -T \"{0}\"", fileName),
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
        private static Image<Rgba32> LoadImageInternal(string filename)
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
                    Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, format: "Executing : {0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments));

                    return null;
                }

                using (Stream stream = process.StandardOutput.BaseStream)
                {
                    Image<Rgba32> image = null;

                    try
                    {
                        image = ConverterCommon.OpenBitmapFromTiffStream(stream);

                        process.WaitForExit();

                        ImageHelpers.RotateImageIfNecessary(image, degrees: 180);

                        return image;
                    }
                    catch (Exception)
                    {
                        image?.Dispose();

                        throw;
                    }
                }
            }
        }
    }
}