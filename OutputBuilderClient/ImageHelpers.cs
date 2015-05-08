using System;
using System.Diagnostics.Contracts;
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
        public static void RotateImageIfNecessary(MagickImage image, int degrees)
        {
            Contract.Requires(image != null);

            Contract.Requires(degrees == 0 || degrees == 90 || degrees == 180 || degrees == 270);

            switch (degrees)
            {
                case 0: // No need to rotate
                    return;

                case 90: // Rotate 90 degrees clockwise
                    image.Rotate(90);
                    return;

                case 180: // Rotate upside down
                    image.Rotate(180);
                    return;

                case 270: // Rotate 90 degrees anti-clockwise
                    image.Rotate(270);
                    return;

                default: // unknown - so can't rotate;
                    return;
            }
        }

        public static bool IsValidJpegImage(byte[] bytes, string context)
        {
            try
            {
                using (var image = new MagickImage())
                {
                    image.Warning += (sender, e) =>
                        {
                            Console.WriteLine("Image Validate Error: {0}", context);
                            Console.WriteLine("Image Validate Error: {0}", e.Message);
                            throw e.Exception;
                        };

                    image.Read(bytes);

                    return image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Jpg;
                }
            }
            catch (MagickException exception)
            {
                Console.WriteLine("Error: {0}", context);
                Console.WriteLine("Error: {0}", exception);
                return false;
            }
        }
    }
}