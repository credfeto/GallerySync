﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Credfeto.Gallery.Scanner;

public static class DirectoryScanner
{
    public static async Task<long> ScanFolderAsync(string baseFolder, IFileEmitter fileEmitter, IReadOnlyList<string> extensionsToRetrieveInOrderOfPrecendence, IReadOnlyList<string> sidecarFiles)
    {
        Context context = new(baseFolder: baseFolder, fileEmitter: fileEmitter, extensionsToRetrieveInOrderOfPrecendence: extensionsToRetrieveInOrderOfPrecendence, sidecarExtensions: sidecarFiles);

        await StartScanningAsync(context);

        long filesFound = await ProcessEntriesAsync(context);

        return filesFound;
    }

    private static Task StartScanningAsync(Context context)
    {
        return ScanSubFolderAsync(folder: context.BaseFolder, context: context);
    }

    private static async Task<long> ProcessEntriesAsync(Context context)
    {
        long filesFound = 0L;
        bool more = true;

        while (more)
        {
            more = context.FilesToProcess.TryDequeue(out FileEntry? entry);

            if (more && entry != null)
            {
                ++filesFound;
                await context.FileEmitter.FileFoundAsync(entry);
            }
        }

        return filesFound;
    }

    private static async Task ScanSubFolderAsync(string folder, Context context)
    {
        await FindSubFoldersAsync(folder: folder, context: context);

        FindFiles(folder: folder, context: context);
    }

    private static void FindFiles(string folder, Context context)
    {
        string[] raw = Directory.GetFiles(path: folder, searchPattern: "*");

        var grouped = raw.Select(record => new
            {
                record,
                extension = Path.GetExtension(record)
                    .ToLowerInvariant()
            })
            .Where(t => context.ExtensionsToRetrieveInOrderOfPrecendence.Contains(value: t.extension, comparer: StringComparer.OrdinalIgnoreCase))
            .GroupBy(keySelector: t => Path.GetFileNameWithoutExtension(t.record)
                    .ToLowerInvariant(),
                elementSelector: t => t.record,
                comparer: StringComparer.OrdinalIgnoreCase)
            .Where(context.HasRequiredExtensionMatch)
            .Select(matches => new
            {
                BaseName = matches.Key,
                Items = matches.OrderByDescending(keySelector: match => ExtensionScore(scores: context.ExtensionsToRetrieveInOrderOfPrecendence, match: match))
                    .ThenBy(Path.GetExtension)
                    .Select(x => Path.GetFileName(x)!)
            });

        foreach (IReadOnlyList<string> items in grouped.Select(fileGroup => fileGroup.Items.ToArray()))
        {
            string file = items[0];

            context.FilesToProcess.Enqueue(new FileEntry
            {
                Folder = folder,
                RelativeFolder = folder.Substring(context.BaseFolder.Length + 1),
                LocalFileName = file,
                AlternateFileNames = items.Skip(count: 1)
                    .ToList()
            });
        }
    }

    private static int ExtensionScore(IReadOnlyList<string> scores, string match)
    {
        string extension = Path.GetExtension(match)
            .ToLowerInvariant();

        var found = scores.Select((c, i) => new { Ext = c, Index = i })
            .FirstOrDefault(x => extension == x.Ext);

        return found?.Index ?? -1;
    }

    private static Task FindSubFoldersAsync(string folder, Context context)
    {
        string[] folders = Directory.GetDirectories(path: folder, searchPattern: "*")
            .Where(predicate: subFolder => !IsSkipFolderName(subFolder.Substring(folder.Length + 1)))
            .ToArray();

        return Task.WhenAll(folders.Select(selector: subFolder => ScanSubFolderAsync(folder: subFolder, context: context))
            .ToArray());
    }

    private static bool IsSkipFolderName(string folder)
    {
        string[] badFolders = { "Sort", ".SyncArchive", ".git", ".svn" };

        return badFolders.Any(predicate: candidate => StringComparer.InvariantCultureIgnoreCase.Equals(x: folder, y: candidate));
    }

    private sealed class Context
    {
        private readonly Func<IEnumerable<string>, bool> _cannotBeSoleExtensionMatch;

        public Context(string baseFolder, IFileEmitter fileEmitter, IReadOnlyList<string> extensionsToRetrieveInOrderOfPrecendence, IReadOnlyList<string> sidecarExtensions)
        {
            this.BaseFolder = baseFolder;
            this.FileEmitter = fileEmitter;
            this.ExtensionsToRetrieveInOrderOfPrecendence = extensionsToRetrieveInOrderOfPrecendence;
            this._cannotBeSoleExtensionMatch = BuildSidecarProcessor(sidecarExtensions);
        }

        public string BaseFolder { get; }

        public IFileEmitter FileEmitter { get; }

        public IReadOnlyList<string> ExtensionsToRetrieveInOrderOfPrecendence { get; }

        public ConcurrentQueue<FileEntry> FilesToProcess { get; } = new();

        private static Func<IEnumerable<string>, bool> BuildSidecarProcessor(IReadOnlyList<string> sidecarExtensions)
        {
            Func<IEnumerable<string>, bool> sidecarProcessor;

            if (sidecarExtensions.Count != 0)
            {
                sidecarProcessor = matches => matches.Any(predicate: match => IsNotSidecarExtension(sidecarExtensions: sidecarExtensions, match: match));
            }
            else
            {
                sidecarProcessor = _ => true;
            }

            return sidecarProcessor;
        }

        private static bool IsNotSidecarExtension(IReadOnlyList<string> sidecarExtensions, string match)
        {
            return !sidecarExtensions.Contains(Path.GetExtension(match), comparer: StringComparer.OrdinalIgnoreCase);
        }

        public bool HasRequiredExtensionMatch(IEnumerable<string> matches)
        {
            return this._cannotBeSoleExtensionMatch(matches);
        }
    }
}