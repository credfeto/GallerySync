using System;
using System.IO;
using GraphicsMagick;
using StorageHelpers;

namespace DeleteImageIfCorrupt
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
                return -1;

            if (File.Exists(args[0]))
                try
                {
                    var data = File.ReadAllBytes(args[0]);

                    if (IsValidJpegImage(data, args[0]))
                    {
                        return 0;
                    }
                    FileHelpers.DeleteFile(args[0]);
                    return 2;
                }
                catch (Exception)
                {
                    FileHelpers.DeleteFile(args[0]);
                    return 2;
                }

            return 0;
        }

        private static bool IsValidJpegImage(byte[] bytes, string context)
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