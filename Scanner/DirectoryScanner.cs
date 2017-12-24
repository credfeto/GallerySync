using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Twaddle.Directory.Scanner
{
    public static class DirectoryScanner
    {
        public static async Task<long> ScanFolder(string baseFolder, IFileEmitter fileEmitter,
            List<string> extensionsToRetrieveInOrderOfPrecendence,
            List<string> sidecarFiles)
        {
            var context = new Context(baseFolder, fileEmitter, extensionsToRetrieveInOrderOfPrecendence, sidecarFiles);

            await StartScanning(context);

            var filesFound = await ProcessEntries(context);

            return filesFound;
        }

        private static Task StartScanning(Context context)
        {
            return ScanSubFolder(context.BaseFolder, context);
        }

        private static async Task<long> ProcessEntries(Context context)
        {
            var filesFound = 0L;
            var more = true;
            while (more)
            {
                FileEntry entry;
                more = context.FilesToProcess.TryDequeue(out entry);
                if (more)
                {
                    ++filesFound;
                    await context.FileEmitter.FileFound(entry);
                }
            }

            return filesFound;
        }

        private static async Task ScanSubFolder(string folder, Context context)
        {
            await FindSubFolders(folder, context);

            FindFiles(folder, context);
        }

        private static void FindFiles(string folder, Context context)
        {
            var raw = System.IO.Directory.GetFiles(folder, "*");

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
                var file = fileGroup.Items.First();

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

        private static Task FindSubFolders(string folder, Context context)
        {
            var folders = System.IO.Directory.GetDirectories(folder, "*")
                .Where(subFolder => !IsSkipFolderName(subFolder.Substring(folder.Length + 1)))
                .ToArray();

            return Task.WhenAll(
                folders.Select(subFolder => ScanSubFolder(subFolder, context)).ToArray());
        }

        private static bool IsSkipFolderName(string folder)
        {
            var badFolders = new[] {"Sort", ".SyncArchive", ".git", ".svn"};

            return badFolders.Any(candidate => StringComparer.InvariantCultureIgnoreCase.Equals(folder, candidate));
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
                    sidecarProcessor =
                        matches => matches.Any(
                            match => IsNotSidecarExtension(sidecarExtensions, match));
                else
                    sidecarProcessor = matches => true;
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