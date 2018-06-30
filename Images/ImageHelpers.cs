﻿using System;
using System.Diagnostics.Contracts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Images
{
    internal static class ImageHelpers
    {
        public static bool IsValidJpegImage(byte[] bytes, string context)
        {
            try
            {
                using (Image.Load(bytes, out var format))
                {
                    return format.DefaultMimeType == "image/jpeg";
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: {0}", context);
                Console.WriteLine("Error: {0}", exception);
                return false;
            }
        }

        /// <summary>
        ///     Rotates the image if necessary.
        /// </summary>
        /// <param name="image">
        ///     The image.
        /// </param>
        /// <param name="degrees">
        ///     The degrees to rotate.
        /// </param>
        /// <remarks>
        ///     Only 0, 90, 180 and 270 degrees are supported.
        /// </remarks>
        public static void RotateImageIfNecessary<TPixel>(Image<TPixel> image, int degrees)
            where TPixel : struct, IPixel<TPixel>
        {
            Contract.Requires(image != null);

            Contract.Requires(degrees == 0 || degrees == 90 || degrees == 180 || degrees == 270);

            switch (degrees)
            {
                case 0: // No need to rotate
                    return;

                case 90: // Rotate 90 degrees clockwise
                    image.Mutate(ctx => ctx.Rotate(90));
                    return;

                case 180: // Rotate upside down
                    image.Mutate(ctx => ctx.Rotate(180));
                    return;

                case 270: // Rotate 90 degrees anti-clockwise
                    image.Mutate(ctx => ctx.Rotate(270));
                    return;

                default: // unknown - so can't rotate;
                    return;
            }
        }
    }
}