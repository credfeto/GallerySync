namespace OutputBuilderClient
{
    using System;
    using System.IO;

    using TagLib;

    internal static class FileHelpers
    {
        public static void WriteAllBytes(string fileName, byte[] bytes)
        {
            const int maxRetries = 5;

            WriteWithRetries(fileName, bytes, maxRetries);
        }

        private static void DeleteFile(string fileName)
        {
            try
            {
                Alphaleonis.Win32.Filesystem.File.Delete(fileName);
            }
            catch
            {
            }
        }

        private static void RemoveExistingFile(string fileName)
        {
            if (Alphaleonis.Win32.Filesystem.File.Exists(fileName))
            {
                try
                {
                    DeleteFile(fileName);
                }
                catch
                {
                    // Don't care if it fails
                }
            }
        }

        private static void VerifyContent(string path, byte[] bytes)
        {
            var written = Alphaleonis.Win32.Filesystem.File.ReadAllBytes(path);
            if (bytes.Length != written.Length)
            {
                throw new CorruptFileException(
                    string.Format(
                        "File {0} does not contain the bytes that were written (size different Src:{1} != Dest:{2})",
                        path,
                        bytes.Length,
                        written.Length));
            }

            for (int pos = 0; pos < bytes.Length; ++pos)
            {
                if (bytes[pos] != written[pos])
                {
                    throw new CorruptFileException(
                        string.Format(
                            "File {0} does not contain the bytes that were written (different at position {1} Src:{2} != Dest:{3})",
                            path,
                            pos,
                            bytes[pos],
                            written[pos]));
                }
            }
        }

        private static void WriteContent(string path, byte[] bytes)
        {
            WriteNoVerify(path, bytes);

            VerifyContent(path, bytes);
        }

        private static void WriteNoVerify(string path, byte[] bytes)
        {
            using (
                var fileStream = new FileStream(
                    mode: FileMode.Create,
                    access: FileAccess.Write,
                    share: FileShare.None,
                    path: path,
                    bufferSize: 8192,
                    useAsync: false))
            {
                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        private static void WriteWithRetries(string fileName, byte[] data, int maxRetries)
        {
            int retries = 0;
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
                    Console.WriteLine("Stack:");
                    Console.WriteLine(exception.StackTrace);
                    DeleteFile(fileName);
                }

                ++retries;
            }
        }
    }
}