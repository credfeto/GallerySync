using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using LibGit2Sharp;
using Polly;

namespace StorageHelpers
{
    public static class FileHelpers
    {
        private const int RETRY_ATTEMPTS = 4;
        private static readonly SemaphoreSlim CommitLock = new SemaphoreSlim(initialCount: 1);
        private static readonly BufferBlock<Func<Task>> CommitBufferBlock = CreateForProcessing();

        public static Task WriteAllBytesAsync(string fileName, byte[] bytes)
        {
            const int maxRetries = 5;

            EnsureFolderExists(fileName);

            return WriteWithRetriesAsync(fileName, bytes, maxRetries);
        }

        private static void EnsureFolderExists(string fileName)
        {
            string path = Path.GetDirectoryName(fileName);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static void DeleteFile(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                    CommitBufferBlock.Post(item: () => DoCommitAsync(fileName));
                }
            }
            catch (IOException)
            {
                // Don't care
            }
            catch (UnauthorizedAccessException)
            {
                // Don't care
            }
        }

        private static void RemoveExistingFile(string fileName)
        {
            if (File.Exists(fileName))
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

        private static async Task VerifyContentAsync(string path, byte[] bytes)
        {
            byte[] written = await ReadAllBytesAsync(path);

            if (bytes.Length != written.Length)
            {
                throw new FileContentException(string.Format(format: "File {0} does not contain the bytes that were written (size different Src:{1} != Dest:{2})",
                                                             path,
                                                             bytes.Length,
                                                             written.Length));
            }

            for (int pos = 0; pos < bytes.Length; ++pos)
            {
                if (bytes[pos] != written[pos])
                {
                    throw new FileContentException(string.Format(format: "File {0} does not contain the bytes that were written (different at position {1} Src:{2} != Dest:{3})",
                                                                 path,
                                                                 pos,
                                                                 bytes[pos],
                                                                 written[pos]));
                }
            }
        }

        private static async Task WriteContentAsync(string path, byte[] bytes)
        {
            if (File.Exists(path))
            {
                byte[] existingBytes = await ReadAllBytesAsync(path);

                if (AreSame(existingBytes, bytes))
                {
                    return;
                }
            }

            await WriteNoVerifyAsync(path, bytes);

            await VerifyContentAsync(path, bytes);
        }

        private static bool AreSame(byte[] existingBytes, byte[] bytesToWrite)
        {
            if (existingBytes.Length != bytesToWrite.Length)
            {
                return false;
            }

            for (int pos = 0; pos < existingBytes.Length; ++pos)
            {
                if (existingBytes[pos] != bytesToWrite[pos])
                {
                    return false;
                }
            }

            return true;
        }

        private static Task WriteNoVerifyAsync(string path, byte[] bytes)
        {
            return File.WriteAllBytesAsync(path, bytes);
        }

        private static async Task WriteWithRetriesAsync(string fileName, byte[] data, int maxRetries)
        {
            try
            {
                int retries = 0;

                while (retries < maxRetries)
                {
                    RemoveExistingFile(fileName);

                    try
                    {
                        await WriteContentAsync(fileName, data);

                        return;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(format: "Error: {0}", exception.Message);
                        Console.WriteLine(format: "File: {0}", fileName);
                        Console.WriteLine(format: "Attempt: {0} of {1}", retries + 1, maxRetries);
                        Console.WriteLine(value: "Stack:");
                        Console.WriteLine(exception.StackTrace);
                    }

                    await Task.Delay(millisecondsDelay: 500);
                    RemoveExistingFile(fileName);
                    await Task.Delay(millisecondsDelay: 1500);

                    ++retries;
                }
            }
            finally
            {
                CommitBufferBlock.Post(item: () => DoCommitAsync(fileName));
            }
        }

        private static async Task DoCommitAsync(string fileName)
        {
            await CommitLock.WaitAsync();

            try
            {
                string workDir = Path.GetDirectoryName(fileName);

                using (Repository repo = OpenRepository(workDir))
                {
                    string localFile = GetLocalRepoFile(repo, fileName);

                    // Stage the file
                    repo.Index.Add(localFile);
                    repo.Index.Write();

                    Signature author = new Signature(name: "Mark Ridgwell", email: "@credfeto@users.noreply.github.com", DateTime.UtcNow);
                    Signature committer = author;

                    repo.Commit($"Updated {localFile}", author, committer);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Failed to commit: {exception.Message}");
            }
            finally
            {
                CommitLock.Release();
            }
        }

        private static string GetLocalRepoFile(Repository repo, string fileName)
        {
            return fileName.Substring(repo.Info.WorkingDirectory.Length);
        }

        private static Repository OpenRepository(string workDir)
        {
            string found = Repository.Discover(workDir);

            return new Repository(found);
        }

        public static Task<byte[]> ReadAllBytesAsync(string filename)
        {
            return File.ReadAllBytesAsync(filename);
        }

        // <summary>
        /// Creates a buffer block for processing tasks.
        /// </summary>
        /// <returns>The buffer block.</returns>
        public static BufferBlock<Func<Task>> CreateForProcessing()
        {
            IAsyncPolicy policy = Policy.Handle<Exception>()
                                        .RetryAsync(RETRY_ATTEMPTS);

            async Task Execute(Func<Task> action)
            {
                try
                {
                    await policy.ExecuteAsync(action);
                }
                catch
                {
                    // Don't break the buffer block by letting exceptions escape.
                }
            }

            BufferBlock<Func<Task>> bufferBlock = new BufferBlock<Func<Task>>();

            // link the buffer block to an action block to process the actions submitted to the buffer.
            // restrict the number of parallel tasks executing to 1
            // tasks submitted here from consuming all the available CPU time.
            bufferBlock.LinkTo(new ActionBlock<Func<Task>>(Execute, new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = 1, MaxMessagesPerTask = 1, BoundedCapacity = 1}));

            return bufferBlock;
        }
    }
}