using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
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
        public static Image<Rgba32> OpenBitmapFromStream(Stream stream)
        {
            Contract.Requires(stream != null);

            Image<Rgba32> image = null;

            try
            {
                image = Image.Load(stream);

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