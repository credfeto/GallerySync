using System;
using System.Threading;
using Alphaleonis.Win32.Filesystem;
using IOException = System.IO.DirectoryNotFoundException;

namespace StorageHelpers
{
    public static class FileHelpers
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

        public static void DeleteFile(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                    File.Delete(fileName);
            }
            catch (IOException)
            {
            }
            catch (FileReadOnlyException)
            {
            }
            catch (UnauthorizedAccessException)
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
                throw new FileContentException(
                    string.Format(
                        "File {0} does not contain the bytes that were written (size different Src:{1} != Dest:{2})",
                        path,
                        bytes.Length,
                        written.Length));

            for (var pos = 0; pos < bytes.Length; ++pos)
                if (bytes[pos] != written[pos])
                    throw new FileContentException(
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
                    Thread.Sleep(500);
                    DeleteFile(fileName);
                    Thread.Sleep(1500);
                }

                ++retries;
            }
        }

        public static byte[] ReadAllBytes(string filename)
        {
            return File.ReadAllBytes(filename);
        }
    }
}