using System.Collections.Generic;

namespace Credfeto.Gallery.Scanner;

public class FileEntry
{
    public string Folder { get; set; }

    public string RelativeFolder { get; set; }

    public string LocalFileName { get; set; }

    public IReadOnlyList<string> AlternateFileNames { get; set; }
}