using System;
using System.Diagnostics;

namespace Credfeto.Gallery.ObjectModel;

[Serializable]
[DebuggerDisplay(value: "Extension: {Extension}, Hash:{Hash}")]
public sealed class ComponentFile
{
    public string Extension { get; set; } = default!;

    public string Hash { get; set; } = default!;

    public DateTime LastModified { get; set; }

    public long FileSize { get; set; }
}