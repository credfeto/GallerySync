using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using BitMiracle.LibTiff.Classic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Images.Converters
{
    internal static class ConverterCommon
    {
        /// <summary>
        ///     Opens the bitmap from stream.
        /// </summary>
        /// <param name="stream">
        ///     The stream.
        /// </param>
        /// <returns>
        ///     The image that was contained in the stream.
        /// </returns>
        [SuppressMessage(category: "Microsoft.Usage", checkId: "CA1801:ReviewUnusedParameters", MessageId = "fileName", Justification = "Used for logging")]
        public static Image<Rgba32> OpenBitmapFromTiffStream(Stream stream)
        {
            Contract.Requires(stream != null);

            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);

                using (Tiff tif = Tiff.ClientOpen(name: "in-memory", mode: "r", ms, new TiffStream()))
                {
                    // Find the width and height of the image
                    FieldValue[] value = tif.GetField(TiffTag.IMAGEWIDTH);
                    int width = value[0]
                        .ToInt();

                    value = tif.GetField(TiffTag.IMAGELENGTH);
                    int height = value[0]
                        .ToInt();

                    // Read the image into the memory buffer
                    int[] raster = new int[height * width];

                    if (!tif.ReadRGBAImage(width, height, raster))
                    {
                        return null;
                    }

                    bool ok = false;
                    Image<Rgba32> bmp = null;

                    try
                    {
                        bmp = new Image<Rgba32>(width, height);

                        for (int y = 0; y < bmp.Height; y++)
                        {
                            int rasterOffset = y * bmp.Width;

                            for (int x = 0; x < bmp.Width; x++)
                            {
                                int rgba = raster[rasterOffset++];
                                byte r = (byte) ((rgba >> 16) & 0xff);
                                byte g = (byte) ((rgba >> 8) & 0xff);
                                byte b = (byte) (rgba & 0xff);
                                byte a = (byte) ((rgba >> 24) & 0xff);

                                bmp[x, y] = new Rgba32(r, g, b, a);
                            }
                        }

                        ok = true;
                        return bmp;
                    }
                    finally
                    {
                        if(!ok)
                        bmp?.Dispose();

                    }
                }
            }
       }
    }
}