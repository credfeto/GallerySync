using System.IO;
using OutputBuilderClient;
using SixLabors.ImageSharp;

namespace Images.Converters
{
    /*
    /// <summary>
    ///     Image converter that uses RawPhotoshop.
    /// </summary>
    [SupportedExtension("psd")]
    internal class PhotoshopImageConverter : IImageConverter
    {
        public Image<Rgba32> LoadImage(string fileName)
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
            if (psdFile == null)
            {
                throw new IOException( "PsdFile did not load file.");
            }
            
            return psdFile;
        }
    }
    */
}