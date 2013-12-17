using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Twaddle.Directory.Scanner
{
    public static class DirectoryScanner
    {
        public static long ScanFolder(string baseFolder, IFileEmitter fileEmitter,
                                      List<string> extensionsToRetrieveInOrderOfPrecendence,
                                      List<string> sidecarFiles)
        {
            var context = new Context(baseFolder, fileEmitter, extensionsToRetrieveInOrderOfPrecendence, sidecarFiles);

            using (var ewh = new EventWaitHandle(false, EventResetMode.ManualReset))
            {
                var t = new Thread(() => StartScanning(context, ewh));
                t.Start();

                ewh.WaitOne();

                const int waitTime = 1000;

                long filesFound = 0L;
                bool completed = t.Join(waitTime);
                while (!completed)
                {
                    filesFound += ProcessEntries(context);

                    completed = t.Join(waitTime);
                }

                filesFound += ProcessEntries(context);

                return filesFound;
            }
        }

        private static void StartScanning(Context context, EventWaitHandle ewh)
        {
            ewh.Set();
            ScanSubFolder(context.BaseFolder, context);
        }

        private static long ProcessEntries(Context context)
        {
            long filesFound = 0L;
            bool more = true;
            while (more)
            {
                FileEntry entry;
                more = context.FilesToProcess.TryDequeue(out entry);
                if (more)
                {
                    ++filesFound;
                    context.FileEmitter.FileFound(entry);
                }
            }

            return filesFound;
        }

        private static void ScanSubFolder(string folder, Context context)
        {
            FindSubFolders(folder, context);

            FindFiles(folder, context);
        }

        private static void FindFiles(string folder, Context context)
        {
            string[] raw = System.IO.Directory.GetFiles(folder, "*");


            var grouped = from record in raw
                          let extension = Path.GetExtension(record).ToLowerInvariant()
                          where context.ExtensionsToRetrieveInOrderOfPrecendence.Contains(extension)
                          group record by Path.GetFileNameWithoutExtension(record).ToLowerInvariant()
                          into matches
                          where context.HasRequiredExtensionMatch(matches)
                          select new
                              {
                                  BaseName = matches.Key,
                                  Items = matches.OrderByDescending(match =>
                                                                    ExtensionScore(
                                                                        context.ExtensionsToRetrieveInOrderOfPrecendence,
                                                                        match))
                                                 .ThenBy(Path.GetExtension).Select(match => Path.GetFileName(match))
                              };

            foreach (var fileGroup in grouped)
            {
                string file = fileGroup.Items.First();

                context.FilesToProcess.Enqueue(new FileEntry
                    {
                        Folder = folder,
                        RelativeFolder = folder.Substring(context.BaseFolder.Length + 1),
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

        private static void FindSubFolders(string folder, Context context)
        {
            foreach (string subFolder in System.IO.Directory.GetDirectories(folder, "*"))
            {
                string leaf = subFolder.Substring(folder.Length + 1);

                if (IsSkipFolderName(leaf))
                {
                    continue;
                }

                ScanSubFolder(subFolder, context);
            }
        }

        private static bool IsSkipFolderName(string folder)
        {
            return StringComparer.InvariantCultureIgnoreCase.Equals(folder, "Sort");
        }

        private sealed class Context
        {
            private readonly string _baseFolder;
            private readonly Func<IEnumerable<string>, bool> _cannotBeSoleExtensionMatch;
            private readonly List<string> _extensionsToRetrieveInOrderOfPrecendence;
            private readonly IFileEmitter _fileEmitter;
            private readonly ConcurrentQueue<FileEntry> _filesToProcess = new ConcurrentQueue<FileEntry>();

            public Context(string baseFolder, IFileEmitter fileEmitter,
                           List<string> extensionsToRetrieveInOrderOfPrecendence,
                           List<string> sidecarExtensions)
            {
                _baseFolder = baseFolder;
                _fileEmitter = fileEmitter;
                _extensionsToRetrieveInOrderOfPrecendence = extensionsToRetrieveInOrderOfPrecendence;
                _cannotBeSoleExtensionMatch = BuildSidecarProcessor(sidecarExtensions);
            }

            public string BaseFolder
            {
                get { return _baseFolder; }
            }

            public IFileEmitter FileEmitter
            {
                get { return _fileEmitter; }
            }

            public List<string> ExtensionsToRetrieveInOrderOfPrecendence
            {
                get { return _extensionsToRetrieveInOrderOfPrecendence; }
            }

            public ConcurrentQueue<FileEntry> FilesToProcess
            {
                get { return _filesToProcess; }
            }

            private static Func<IEnumerable<string>, bool> BuildSidecarProcessor(List<string> sidecarExtensions)
            {
                Func<IEnumerable<string>, bool> sidecarProcessor;
                if (sidecarExtensions.Any())
                {
                    sidecarProcessor =
                        matches => matches.Any(
                            match => IsNotSidecarExtension(sidecarExtensions, match));
                }
                else
                {
                    sidecarProcessor = matches => true;
                }
                return sidecarProcessor;
            }

            private static bool IsNotSidecarExtension(List<string> sidecarExtensions, string match)
            {
                return !sidecarExtensions.Contains(Path.GetExtension(match).ToLowerInvariant());
            }

            public bool HasRequiredExtensionMatch(IEnumerable<string> matches)
            {
                return _cannotBeSoleExtensionMatch(matches);
            }
        }
    }
}