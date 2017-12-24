using System;
using System.IO;
using System.Threading.Tasks;

namespace StorageHelpers
{
    public static class FileHelpers
    {
        public static Task WriteAllBytes(string fileName, byte[] bytes)
        {
            const int maxRetries = 5;

            EnsureFolderExists(fileName);

            return WriteWithRetries(fileName, bytes, maxRetries);
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

        private static async Task VerifyContent(string path, byte[] bytes)
        {
            var written = await ReadAllBytes(path);
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

        private static async Task WriteContent(string path, byte[] bytes)
        {
            if (File.Exists(path))
            {
                var existingBytes = await ReadAllBytes(path);

                if (AreSame(existingBytes, bytes))
                    return;
            }
            await WriteNoVerify(path, bytes);

            await VerifyContent(path, bytes);
        }

        private static bool AreSame(byte[] existingBytes, byte[] bytesToWrite)
        {
            if (existingBytes.Length != bytesToWrite.Length)
                return false;

            for (var pos = 0; pos < existingBytes.Length; ++pos)
                if (existingBytes[pos] != bytesToWrite[pos])
                    return false;

            return true;
        }

        private static Task WriteNoVerify(string path, byte[] bytes)
        {
            return Task.Run(() => File.WriteAllBytes(path, bytes));
        }

        private static async Task WriteWithRetries(string fileName, byte[] data, int maxRetries)
        {
            var retries = 0;
            while (retries < maxRetries)
            {
                RemoveExistingFile(fileName);

                try
                {
                    await WriteContent(fileName, data);

                    return;
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Error: {0}", exception.Message);
                    Console.WriteLine("File: {0}", fileName);
                    Console.WriteLine("Attempt: {0} of {1}", retries + 1, maxRetries);
                    Console.WriteLine("Stack:");
                    Console.WriteLine(exception.StackTrace);
                }

                await Task.Delay(500);
                DeleteFile(fileName);
                await Task.Delay(1500);

                ++retries;
            }
        }

        public static Task<byte[]> ReadAllBytes(string filename)
        {
            return
                Task.Run(() =>
                    File.ReadAllBytes(filename));
        }
    }
}