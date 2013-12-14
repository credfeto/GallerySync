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

            try
            {
                return new Bitmap(fileName);
            }
            catch (OutOfMemoryException)
            {
                return null;
            }
        }

        #endregion

        #endregion
    }
}