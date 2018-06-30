using System.Drawing;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Drawing.Brushes;

namespace Images
{
    public static class QrCode
    {
        public static Image<Rgba32> EncodeUrl(string url, int height)
        {
            //url = "https://www.markridgwell.co.uk/";
            var encoder = new QrEncoder(ErrorCorrectionLevel.H);
            QrCode qr;

            if (encoder.TryEncode(url, out qr))
            {
                int moduleSize = ImageExtraction.CaclulateQrModuleSize(height);

                using (MemoryStream stream = new MemoryStream())
                {
                    var darkBrush = Brushes.Black;
                    var lightBrush = new SolidBrush(Color.FromArgb(alpha: 128, red: 255, green: 255, blue: 255));

                    var renderer = new GraphicsRenderer(new FixedModuleSize(moduleSize, QuietZoneModules.Two), darkBrush, lightBrush);

                    renderer.WriteToStream(qr.Matrix, ImageFormat.Png, stream);

                    return Image.Load(stream.ToArray());
                }
            }

            return null;
        }
    }
}