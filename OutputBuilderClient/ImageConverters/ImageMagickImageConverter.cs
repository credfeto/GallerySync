// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageMagickImageConverter.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Image converter that uses ImageMagick.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace OutputBuilderClient.ImageConverters
{
    using System;

    using GraphicsMagick;

    /// <summary>
    ///     Image converter that uses ImageMagick.
    /// </summary>
    [SupportedExtension("psd")]
    [SupportedExtension("tga")]
    [SupportedExtension("jpg")]
    [SupportedExtension("jpeg")]
    [SupportedExtension("jpe")]
    [SupportedExtension("gif")]
    [SupportedExtension("tif")]
    [SupportedExtension("tiff")]
    [SupportedExtension("png")]
    [SupportedExtension("bmp")]
    internal class ImageMagickImageConverter : IImageConverter
    {
        public MagickImage LoadImage(string fileName)
        {
            MagickImage image = null;

            try
            {
                image = new MagickImage();

                image.Warning += (sender, e) =>
                    {
                        Console.WriteLine("Image Load Error: {0}", e.Message);
                        throw e.Exception;
                    };

                image.Read(fileName);

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