using System;
using System.IO;
using System.Security.Cryptography;

namespace FileNaming
{
    public static class Hasher
    {
        public static string HashFile(string filename)
        {
            byte[] bytes = File.ReadAllBytes(filename);
            return HashBytes(bytes);
        }

        public static string HashBytes(byte[] bytes)
        {
            using (SHA512 hasher = SHA512.Create())
            {
                return BitConverter.ToString(hasher.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}