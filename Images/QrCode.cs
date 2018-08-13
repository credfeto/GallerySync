using System.IO;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Images
{
    public static class QrCode
    {
        public static Image<Rgba32> EncodeUrl(string url, int height)
        {
            //url = "https://www.markridgwell.co.uk/";

            try
            {
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.H);
                PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);

                int moduleSize = CaclulateQrModuleSize(height);

                byte[] data = qrCode.GetGraphic(moduleSize);

                using (MemoryStream stream = new MemoryStream(data, writable: false))
                {
                    return Image.Load(stream.ToArray());
                }
            }
            catch
            {
                return null;
            }
        }

        private static int CaclulateQrModuleSize(int height)
        {
            //var moduleSize = height/33;
            //if (height%33 != 0)
            //{
            //    moduleSize += 1;
            //}
            //return moduleSize;
            return 2;
        }
    }
}