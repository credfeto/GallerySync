using System.IO;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Images
{
    public static class QrCode
    {
        private const int QR_MODULE_SIZE = 2;

        public static Image<Rgba32> EncodeUrl(string url)
        {
            //url = "https://www.markridgwell.co.uk/";

            try
            {
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.H);

                    using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                    {
                        int moduleSize = QR_MODULE_SIZE;

                        byte[] data = qrCode.GetGraphic(moduleSize);

                        using (MemoryStream stream = new MemoryStream(data, writable: false))
                        {
                            return Image.Load(stream.ToArray());
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}