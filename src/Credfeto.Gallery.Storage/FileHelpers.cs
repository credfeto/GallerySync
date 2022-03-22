using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace Credfeto.Gallery.Storage;

public static class FileHelpers
{
    private static readonly SemaphoreSlim CommitLock = new(initialCount: 1);

    public static Task WriteAllBytesAsync(string fileName, byte[] bytes, bool commit)
    {
        const int maxRetries = 5;

        EnsureFolderExists(fileName);

        return WriteWithRetriesAsync(fileName: fileName, data: bytes, maxRetries: maxRetries, commit: commit);
    }

    private static void EnsureFolderExists(string fileName)
    {
        string path = Path.GetDirectoryName(fileName)!;

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

                // CommitFileChange(fileName);
            }
        }
        catch (IOException exception)
        {
            // Don't care
            Console.WriteLine(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            // Don't care
            Console.WriteLine(exception.Message);
        }
    }

    private static Task CommitFileChangeAsync(string fileName)
    {
        return DoCommitAsync(fileName);
    }

    private static void RemoveExistingFile(string fileName)
    {
        if (File.Exists(fileName))
        {
            try
            {
                DeleteFile(fileName);
            }
            catch (Exception exception)
            {
                // Don't care if it fails
                Console.WriteLine(exception.Message);
            }
        }
    }

    private static async Task VerifyContentAsync(string path, byte[] bytes)
    {
        byte[] written = await ReadAllBytesAsync(path);

        if (bytes.Length != written.Length)
        {
            throw new FileContentException($"File {path} does not contain the bytes that were written (size different Src:{bytes.Length} != Dest:{written.Length})");
        }

        for (int pos = 0; pos < bytes.Length; ++pos)
        {
            if (bytes[pos] != written[pos])
            {
                throw new FileContentException($"File {path} does not contain the bytes that were written (different at position {pos} Src:{bytes[pos]} != Dest:{written[pos]})");
            }
        }
    }

    private static async Task<bool> WriteContentAsync(string path, byte[] bytes)
    {
        if (File.Exists(path))
        {
            byte[] existingBytes = await ReadAllBytesAsync(path);

            if (AreSame(existingBytes: existingBytes, bytesToWrite: bytes))
            {
                return false;
            }
        }

        await WriteNoVerifyAsync(path: path, bytes: bytes);

        await VerifyContentAsync(path: path, bytes: bytes);

        return true;
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
        return File.WriteAllBytesAsync(path: path, bytes: bytes);
    }

    private static async Task WriteWithRetriesAsync(string fileName, byte[] data, int maxRetries, bool commit)
    {
        bool changed = false;

        try
        {
            int retries = 0;

            while (retries < maxRetries)
            {
                RemoveExistingFile(fileName);

                try
                {
                    changed = await WriteContentAsync(path: fileName, bytes: data);

                    return;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(format: "Error: {0}", arg0: exception.Message);
                    Console.WriteLine(format: "File: {0}", arg0: fileName);
                    Console.WriteLine(format: "Attempt: {0} of {1}", retries + 1, arg1: maxRetries);
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
            if (changed && commit)
            {
                await CommitFileChangeAsync(fileName);
            }
        }
    }

    private static async Task DoCommitAsync(string fileName)
    {
        await CommitLock.WaitAsync();

        string[] alwaysAddFiles = { "ShortUrls.csv", "ShortUrls.csv.tracking.json" };

        try
        {
            string workDir = Path.GetDirectoryName(fileName)!;

            using (Repository repo = OpenRepository(workDir))
            {
                string localFile = GetLocalRepoFile(repo: repo, fileName: fileName);

                foreach (string alwaysAddFile in alwaysAddFiles.Where(alwaysAddFile => File.Exists(Path.Combine(path1: repo.Info.WorkingDirectory, path2: alwaysAddFile))))
                {
                    Commands.Stage(repository: repo, path: alwaysAddFile);
                }

                Commands.Stage(repository: repo, path: localFile);

                Signature author = new("Mark Ridgwell", "@credfeto@users.noreply.github.com", CurrentTime());
                Signature committer = author;

                repo.Commit($"Updated {localFile}", author: author, committer: committer);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to commit: {exception.Message} => {fileName}");
        }
        finally
        {
            CommitLock.Release();
        }
    }

    [SuppressMessage("FunFair.CodeAnalysis", "FFS0002: Use an testable abstraction rather than DateTime.UtcNow", Justification = "This is the abstraction")]
    private static DateTime CurrentTime()
    {
        return DateTime.UtcNow;
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
}