// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IImageConverter.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Image converter.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace OutputBuilderClient
{
    using GraphicsMagick;

    /// <summary>
    ///     Image converter.
    /// </summary>
    public interface IImageConverter
    {
        /// <summary>
        ///     Loads the image.
        /// </summary>
        /// <param name="fileName">
        ///     The filename.
        /// </param>
        /// <returns>
        ///     An image, if it could be loaded.
        /// </returns>
        MagickImage LoadImage(string fileName);
    }
}