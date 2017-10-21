using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using StorageHelpers;

namespace FileNaming
{
    public static class Hasher
    {
        public static async Task<string> HashFile(string filename)
        {
            var bytes = await FileHelpers.ReadAllBytes(filename);
            return HashBytes(bytes);
        }

        public static string HashBytes(byte[] bytes)
        {
            using (var hasher = SHA512.Create())
            {
                return BitConverter.ToString(hasher.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}