// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IImageConverter.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Image converter.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives

#endregion

using System.Drawing;

namespace OutputBuilderClient
{
    /// <summary>
    ///     Image converter.
    /// </summary>
    public interface IImageConverter
    {
        #region Public Methods

        /// <summary>
        ///     Loads the image.
        /// </summary>
        /// <param name="fileName">
        ///     The filename.
        /// </param>
        /// <returns>
        ///     An image, if it could be loaded.
        /// </returns>
        Bitmap LoadImage(string fileName);

        #endregion
    }
}