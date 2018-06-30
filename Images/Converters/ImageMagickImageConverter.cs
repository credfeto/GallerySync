﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageMagickImageConverter.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Image converter that uses ImageMagick.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Images.Converters
{
    /// <summary>
    ///     Image converter that uses ImageMagick.
    /// </summary>
    [SupportedExtension(extension: "tga")]
    [SupportedExtension(extension: "jpg")]
    [SupportedExtension(extension: "jpeg")]
    [SupportedExtension(extension: "jpe")]
    [SupportedExtension(extension: "gif")]
    [SupportedExtension(extension: "tif")]
    [SupportedExtension(extension: "tiff")]
    [SupportedExtension(extension: "png")]
    [SupportedExtension(extension: "bmp")]
    internal class ImageMagickImageConverter : IImageConverter
    {
        public Image<Rgba32> LoadImage(string fileName)
        {
            Image<Rgba32> image = null;

            try
            {
                image = Image.Load(fileName);

                return image;
            }
            catch
            {
                if (image != null)
                {
                    image.Dispose();
                }

                throw;
            }
        }
    }
}