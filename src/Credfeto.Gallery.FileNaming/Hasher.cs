using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Credfeto.Gallery.Storage;

namespace Credfeto.Gallery.FileNaming
{
    public static class Hasher
    {
        public static async Task<string> HashFileAsync(string filename)
        {
            byte[] bytes = await FileHelpers.ReadAllBytesAsync(filename);

            return HashBytes(bytes);
        }

        public static string HashBytes(byte[] bytes)
        {
            using (SHA512 hasher = SHA512.Create())
            {
                return BitConverter.ToString(hasher.ComputeHash(bytes))
                                   .Replace(oldValue: "-", string.Empty)
                                   .ToLowerInvariant();
            }
        }
    }
}