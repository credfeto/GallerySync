using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Twaddle.Directory.Scanner
{
    internal class Emitter : IFileEmitter
    {
        private readonly ConcurrentQueue<FileEntry> _filesToProcess;

        public Emitter(ConcurrentQueue<FileEntry> filesToProcess)
        {
            _filesToProcess = filesToProcess;
        }

        public void FileFound(FileEntry entry)
        {
            _filesToProcess.Enqueue(entry);
        }
    }

    public class DirectoryScanner
    {
        public static long ScanFolder(string baseFolder, IFileEmitter fileEmitter)
        {
            var filesToProcess = new ConcurrentQueue<FileEntry>();

            var emitter = new Emitter(filesToProcess);

            var ewh = new EventWaitHandle(false, EventResetMode.ManualReset);
            var t = new Thread(() => StartScanning(baseFolder, ewh, filesToProcess));
            t.Start();

            ewh.WaitOne();

            const int waitTime = 1000;

            long filesFound = 0L;
            bool completed = t.Join(waitTime);
            while (!completed)
            {
                filesFound += ProcessEntries(filesToProcess, fileEmitter);

                completed = t.Join(waitTime);
            }

            filesFound += ProcessEntries(filesToProcess, fileEmitter);

            return filesFound;
        }

        private static void StartScanning(string baseFolder, EventWaitHandle ewh, ConcurrentQueue<FileEntry> emitter)
        {
            ewh.Set();
            ScanSubFolder(baseFolder, baseFolder, emitter);
        }

        private static long ProcessEntries(ConcurrentQueue<FileEntry> filesToProcess, IFileEmitter fileEmitter)
        {
            long filesFound = 0L;
            bool more = true;
            while (more)
            {
                FileEntry entry;
                more = filesToProcess.TryDequeue(out entry);
                if (more)
                {
                    ++filesFound;
                    fileEmitter.FileFound(entry);
                }
            }

            return filesFound;
        }

        private static void ScanSubFolder(string folder, string baseFolder, ConcurrentQueue<FileEntry> emitter)
        {
            FindSubFolders(folder, baseFolder, emitter);

            FindFiles(folder, baseFolder, emitter);
        }

        private static void FindFiles(string folder, string baseFolder, ConcurrentQueue<FileEntry> emitter)
        {
            string[] raw = System.IO.Directory.GetFiles(folder, "*");

            List<string> scores = new[]
                {
                    ".jpg",
                    ".cr2",
                    ".mrw",
                    ".rw2",
                    ".tif",
                    ".tiff",
                    ".psd",
                }.ToList();

            var grouped = from record in raw
                          where scores.Contains(Path.GetExtension(record).ToLowerInvariant())
                          group record by Path.GetFileNameWithoutExtension(record).ToLowerInvariant()
                          into matches
                          select new
                              {
                                  BaseName = matches.Key,
                                  Items = matches.OrderByDescending(match =>
                                                                    ExtensionScore(scores, match))
                                                 .ThenBy(Path.GetExtension).Select(match => Path.GetFileName(match))
                              };

            foreach (var fileGroup in grouped)
            {
                //Console.WriteLine(" -- {0}", file);

                //var file = fileGroup.BaseName;
                string file = fileGroup.Items.First();

                emitter.Enqueue(new FileEntry
                    {
                        Folder = folder,
                        RelativeFolder = folder.Substring(baseFolder.Length + 1),
                        LocalFileName = file,
                        AlternateFileNames = fileGroup.Items.Skip(1).ToList()
                    });
            }
        }

        private static int ExtensionScore(List<string> scores, string match)
        {
            return scores
                .IndexOf(
                    Path.GetExtension(match)
                        .ToLowerInvariant());
        }

        private static void FindSubFolders(string folder, string baseFolder, ConcurrentQueue<FileEntry> emitter)
        {
            foreach (string subFolder in System.IO.Directory.GetDirectories(folder, "*"))
            {
                string leaf = subFolder.Substring(folder.Length + 1);

                if (IsSkipFolderName(leaf))
                {
                    continue;
                }

                ScanSubFolder(subFolder, baseFolder, emitter);
            }
        }

        private static bool IsSkipFolderName(string folder)
        {
            return StringComparer.InvariantCultureIgnoreCase.Equals(folder, "Sort");
        }
    }
}