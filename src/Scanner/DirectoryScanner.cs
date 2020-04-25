using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scanner
{
    public static class DirectoryScanner
    {
        public static async Task<long> ScanFolderAsync(string baseFolder,
                                                       IFileEmitter fileEmitter,
                                                       List<string> extensionsToRetrieveInOrderOfPrecendence,
                                                       List<string> sidecarFiles)
        {
            Context context = new Context(baseFolder, fileEmitter, extensionsToRetrieveInOrderOfPrecendence, sidecarFiles);

            await StartScanningAsync(context);

            long filesFound = await ProcessEntriesAsync(context);

            return filesFound;
        }

        private static Task StartScanningAsync(Context context)
        {
            return ScanSubFolderAsync(context.BaseFolder, context);
        }

        private static async Task<long> ProcessEntriesAsync(Context context)
        {
            long filesFound = 0L;
            bool more = true;

            while (more)
            {
                more = context.FilesToProcess.TryDequeue(out FileEntry entry);

                if (more)
                {
                    ++filesFound;
                    await context.FileEmitter.FileFoundAsync(entry);
                }
            }

            return filesFound;
        }

        private static async Task ScanSubFolderAsync(string folder, Context context)
        {
            await FindSubFoldersAsync(folder, context);

            FindFiles(folder, context);
        }

        private static void FindFiles(string folder, Context context)
        {
            string[] raw = Directory.GetFiles(folder, searchPattern: "*");

            var grouped = from record in raw
                          let extension = Path.GetExtension(record)
                                              .ToLowerInvariant()
                          where context.ExtensionsToRetrieveInOrderOfPrecendence.Contains(extension)
                          group record by Path.GetFileNameWithoutExtension(record)
                                              .ToLowerInvariant()
                          into matches
                          where context.HasRequiredExtensionMatch(matches)
                          select new
                                 {
                                     BaseName = matches.Key,
                                     Items = matches.OrderByDescending(keySelector: match => ExtensionScore(context.ExtensionsToRetrieveInOrderOfPrecendence, match))
                                                    .ThenBy(Path.GetExtension)
                                                    .Select(selector: match => Path.GetFileName(match))
                                 };

            foreach (var fileGroup in grouped)
            {
                string file = fileGroup.Items.First();

                context.FilesToProcess.Enqueue(new FileEntry
                                               {
                                                   Folder = folder,
                                                   RelativeFolder = folder.Substring(context.BaseFolder.Length + 1),
                                                   LocalFileName = file,
                                                   AlternateFileNames = fileGroup.Items.Skip(count: 1)
                                                                                 .ToList()
                                               });
            }
        }

        private static int ExtensionScore(List<string> scores, string match)
        {
            return scores.IndexOf(Path.GetExtension(match)
                                      .ToLowerInvariant());
        }

        private static Task FindSubFoldersAsync(string folder, Context context)
        {
            string[] folders = Directory.GetDirectories(folder, searchPattern: "*")
                                        .Where(predicate: subFolder => !IsSkipFolderName(subFolder.Substring(folder.Length + 1)))
                                        .ToArray();

            return Task.WhenAll(folders.Select(selector: subFolder => ScanSubFolderAsync(subFolder, context))
                                       .ToArray());
        }

        private static bool IsSkipFolderName(string folder)
        {
            string[] badFolders = {"Sort", ".SyncArchive", ".git", ".svn"};

            return badFolders.Any(predicate: candidate => StringComparer.InvariantCultureIgnoreCase.Equals(folder, candidate));
        }

        private sealed class Context
        {
            private readonly Func<IEnumerable<string>, bool> _cannotBeSoleExtensionMatch;

            public Context(string baseFolder, IFileEmitter fileEmitter, List<string> extensionsToRetrieveInOrderOfPrecendence, List<string> sidecarExtensions)
            {
                this.BaseFolder = baseFolder;
                this.FileEmitter = fileEmitter;
                this.ExtensionsToRetrieveInOrderOfPrecendence = extensionsToRetrieveInOrderOfPrecendence;
                this._cannotBeSoleExtensionMatch = BuildSidecarProcessor(sidecarExtensions);
            }

            public string BaseFolder { get; }

            public IFileEmitter FileEmitter { get; }

            public List<string> ExtensionsToRetrieveInOrderOfPrecendence { get; }

            public ConcurrentQueue<FileEntry> FilesToProcess { get; } = new ConcurrentQueue<FileEntry>();

            private static Func<IEnumerable<string>, bool> BuildSidecarProcessor(List<string> sidecarExtensions)
            {
                Func<IEnumerable<string>, bool> sidecarProcessor;

                if (sidecarExtensions.Any())
                {
                    sidecarProcessor = matches => matches.Any(predicate: match => IsNotSidecarExtension(sidecarExtensions, match));
                }
                else
                {
                    sidecarProcessor = matches => true;
                }

                return sidecarProcessor;
            }

            private static bool IsNotSidecarExtension(List<string> sidecarExtensions, string match)
            {
                return !sidecarExtensions.Contains(Path.GetExtension(match)
                                                       .ToLowerInvariant());
            }

            public bool HasRequiredExtensionMatch(IEnumerable<string> matches)
            {
                return this._cannotBeSoleExtensionMatch(matches);
            }
        }
    }
}