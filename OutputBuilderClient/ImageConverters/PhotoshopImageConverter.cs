using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.PSD;
using System.IO;
using GraphicsMagick;

namespace OutputBuilderClient.ImageConverters
{
    /// <summary>
    ///     Image converter that uses RawPhotoshop.
    /// </summary>
    [SupportedExtension("psd")]
    internal class PhotoshopImageConverter : IImageConverter
    {
        public MagickImage LoadImage(string fileName)
        {
            var psdFile = LoadPsdFile(fileName);

            Bitmap bmp = null;

            try
            {
                bmp = ImageDecoder.DecodeImage(psdFile);
            }
            catch
            {
                if (bmp != null)
                    bmp.Dispose();

                throw;
            }

            using (bmp)
            {
                var imageBytes = ExtractImageToBytes(bmp);

                using (var ms = new MemoryStream(imageBytes, false))
                {
                    return ConverterCommon.OpenBitmapFromStream(ms);
                }
            }
        }

        private static byte[] ExtractImageToBytes(Bitmap bmp)
        {
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);

                imageBytes = ms.ToArray();
            }
            return imageBytes;
        }

        private static PsdFile LoadPsdFile(string fileName)
        {
            var loader = new PsdFile();
            var psdFile = loader.Load(fileName);
            return psdFile;
        }
    }
}