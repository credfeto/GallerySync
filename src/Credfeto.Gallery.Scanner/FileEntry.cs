using System.Collections.Generic;

namespace Credfeto.Gallery.Scanner;

public sealed class FileEntry
{
    public string Folder { get; set; } = default!;

    public string RelativeFolder { get; set; } = default!;

    public string LocalFileName { get; set; } = default!;

    public IReadOnlyList<string> AlternateFileNames { get; set; } = default!;
}