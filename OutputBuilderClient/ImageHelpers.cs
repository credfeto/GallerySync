using System;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using GraphicsMagick;

namespace OutputBuilderClient
{
    internal static class ImageHelpers
    {
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
        public static void RotateImageIfNecessary(Image image, int degrees)
        {
            Contract.Requires(image != null);

            Contract.Requires(degrees == 0 || degrees == 90 || degrees == 180 || degrees == 270);

            switch (degrees)
            {
                case 0: // No need to rotate
                    return;

                case 90: // Rotate 90 degrees clockwise
                    image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    return;

                case 180: // Rotate upside down
                    image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                    return;

                case 270: // Rotate 90 degrees anti-clockwise
                    image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                    return;

                default: // unknown - so can't rotate;
                    return;
            }
        }

        public static bool IsValidJpegImage(byte[] bytes)
        {
            return !ImageIsLoadable(bytes) && ImageIsAJpeg(bytes);
        }

        private static bool ImageIsLoadable(byte[] bytes)
        {
            try
            {
                using (var image = new MagickImage())
                {
                    image.Warning += (sender, e) =>
                        {
                            Console.WriteLine("Image Validate Error: {0}", e.Message);
                            throw e.Exception;
                        };

                    image.Read(bytes);
                }
            }
            catch (MagickException exception)
            {
                Console.WriteLine("Error: {0}", exception);
                return false;
            }

            return true;
        }

        private static bool ImageIsAJpeg(byte[] resizedBytes)
        {
            using (var stream = new MemoryStream(resizedBytes, false))
            {
                using (Image image = Image.FromStream(stream, true, true))
                {
                    var bitmap = (Bitmap) image;
                    if (!bitmap.RawFormat.Equals(ImageFormat.Jpeg))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}