using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using GraphicsMagick;

namespace OutputBuilderClient.ImageConverters
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
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "fileName",
            Justification = "Used for logging")]
        public static MagickImage OpenBitmapFromStream(Stream stream)
        {
            Contract.Requires(stream != null);

            MagickImage image = null;

            try
            {
                image = new MagickImage();

                image.Warning += (sender, e) =>
                {
                    Console.WriteLine("Image Load Error: {0}", e.Message);
                    throw e.Exception;
                };

                image.Read(stream);

                return image;
            }
            catch
            {
                if (image != null)
                    image.Dispose();

                throw;
            }
        }
    }
}