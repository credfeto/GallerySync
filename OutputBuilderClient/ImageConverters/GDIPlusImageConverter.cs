// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GDIPlusImageConverter.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Image converter that uses GDIPlus.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives

using System;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;

#endregion

namespace OutputBuilderClient.ImageConverters
{
    /// <summary>
    ///     Image converter that uses GDIPlus.
    /// </summary>
    [SupportedExtension("jpg")]
    [SupportedExtension("jpeg")]
    [SupportedExtension("jpe")]
    [SupportedExtension("gif")]
    [SupportedExtension("tif")]
    [SupportedExtension("tiff")]
    [SupportedExtension("png")]
    [SupportedExtension("bmp")]
    internal sealed class GdiPlusImageConverter : IImageConverter
    {
        #region Constants and Fields

        /// <summary>
        ///     The Tiff converter.
        /// </summary>
        private static readonly IImageConverter AlternateConverter = new ImageMagickImageConverter();

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
        public Bitmap LoadImage(string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            Bitmap image = LoadImageInternal(fileName);
            if (image != null)
            {
                const int rotationDegrees = 0;

                ImageHelpers.RotateImageIfNecessary(image, rotationDegrees);
            }

            return image;
        }

        private static Bitmap LoadImageInternal(string fileName)
        {
            byte[] data = File.ReadAllBytes(fileName);
            if (data.Length == 0)
            {
                return null;
            }

            try
            {
                using (var stream = new MemoryStream(data, false))
                {
                    return new Bitmap(stream);
                }
            }
            catch (OutOfMemoryException)
            {
                return null;
            }
            catch (Exception)
            {
                Console.WriteLine("Exception when processing file: {0}", fileName);

                return AlternateConverter.LoadImage(fileName);
            }
        }

        #endregion

        #endregion
    }
}