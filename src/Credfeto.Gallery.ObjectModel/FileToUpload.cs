using System;

namespace Credfeto.Gallery.ObjectModel;

[Serializable]
public sealed class FileToUpload
{
    public string FileName { get; set; }

    public bool Completed { get; set; }
}