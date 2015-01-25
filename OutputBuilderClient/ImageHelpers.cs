using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OutputBuilderClient
{
    static class ImageHelpers
    {
        /// <summary>
        /// Rotates the image if necessary.
        /// </summary>
        /// <param name="image">
        /// The image.
        /// </param>
        /// <param name="degrees">
        /// The degrees to rotate.
        /// </param>
        /// <remarks>
        /// Only 0, 90, 180 and 270 degrees are supported.
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

    }
}
