using System;
using Alphaleonis.Win32.Filesystem;
using TagLib;
using File = Alphaleonis.Win32.Filesystem.File;

namespace OutputBuilderClient
{
    internal static class FileHelpers
    {
        public static void WriteAllBytes(string fileName, byte[] bytes)
        {
            const int maxRetries = 5;

            EnsureFolderExists(fileName);

            WriteWithRetries(fileName, bytes, maxRetries);
        }

        private static void EnsureFolderExists(string fileName)
        {
            var path = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void DeleteFile(string fileName)
        {
            try
            {
                File.Delete(fileName);
            }
            catch
            {
            }
        }

        private static void RemoveExistingFile(string fileName)
        {
            if (File.Exists(fileName))
                try
                {
                    DeleteFile(fileName);
                }
                catch
                {
                    // Don't care if it fails
                }
        }

        private static void VerifyContent(string path, byte[] bytes)
        {
            var written = File.ReadAllBytes(path);
            if (bytes.Length != written.Length)
                throw new CorruptFileException(
                    string.Format(
                        "File {0} does not contain the bytes that were written (size different Src:{1} != Dest:{2})",
                        path,
                        bytes.Length,
                        written.Length));

            for (var pos = 0; pos < bytes.Length; ++pos)
                if (bytes[pos] != written[pos])
                    throw new CorruptFileException(
                        string.Format(
                            "File {0} does not contain the bytes that were written (different at position {1} Src:{2} != Dest:{3})",
                            path,
                            pos,
                            bytes[pos],
                            written[pos]));
        }

        private static void WriteContent(string path, byte[] bytes)
        {
            WriteNoVerify(path, bytes);

            VerifyContent(path, bytes);
        }

        private static void WriteNoVerify(string path, byte[] bytes)
        {
            File.WriteAllBytes(path, bytes);
        }

        private static void WriteWithRetries(string fileName, byte[] data, int maxRetries)
        {
            var retries = 0;
            while (retries < maxRetries)
            {
                RemoveExistingFile(fileName);

                try
                {
                    WriteContent(fileName, data);

                    return;
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Error: {0}", exception.Message);
                    Console.WriteLine("File: {0}", fileName);
                    Console.WriteLine("Attempt: {0} of {1}", retries + 1, maxRetries);
                    Console.WriteLine("Stack:");
                    Console.WriteLine(exception.StackTrace);
                    DeleteFile(fileName);
                }

                ++retries;
            }
        }
    }
}