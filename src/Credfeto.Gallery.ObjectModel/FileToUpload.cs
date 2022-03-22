using System;

namespace Credfeto.Gallery.ObjectModel;

[Serializable]
public sealed class FileToUpload
{
    public string FileName { get; set; } = default!;

    public bool Completed { get; set; } = default!;
}