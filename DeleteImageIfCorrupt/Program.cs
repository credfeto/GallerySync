using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using StorageHelpers;

namespace DeleteImageIfCorrupt
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            return AsyncMain(args).GetAwaiter().GetResult();
        }

        private static async Task<int> AsyncMain(string[] args)
        {
            if (args.Length != 1)
                return -1;

            if (File.Exists(args[0]))
                try
                {
                    var data = await FileHelpers.ReadAllBytes(args[0]);

                    if (IsValidJpegImage(data, args[0]))
                        return 0;
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
                using (var image = Image.Load(bytes, out var format ))
                {
                    return format.DefaultMimeType == "image/jpeg";
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: {0}", context);
                Console.WriteLine("Error: {0}", exception);
                return false;
            }
        }
    }
}