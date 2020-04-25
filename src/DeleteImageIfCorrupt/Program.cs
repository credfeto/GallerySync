﻿using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using StorageHelpers;

namespace DeleteImageIfCorrupt
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length != 1)
            {
                return -1;
            }

            if (File.Exists(args[0]))
            {
                try
                {
                    byte[] data = await FileHelpers.ReadAllBytesAsync(args[0]);

                    if (IsValidJpegImage(data))
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
            }

            return 0;
        }

        private static bool IsValidJpegImage(byte[] bytes)
        {
            try
            {
                using (Image.Load(bytes, out IImageFormat format))
                {
                    return format.DefaultMimeType == "image/jpeg";
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(format: "Error: {0}", exception);

                return false;
            }
        }
    }
}