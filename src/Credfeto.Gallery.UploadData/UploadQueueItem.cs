using System;
using System.Diagnostics;

namespace Credfeto.Gallery.UploadData
{
    [Serializable]
    [DebuggerDisplay(value: "UploadType: {UploadType} Path:{Path}, Version: {Version}")]
    public class UploadQueueItem
    {
        public string Path => this.Item.Path;

        public GalleryItem Item { get; set; }

        public UploadType UploadType { get; set; }

        public int Version { get; set; }
    }
}