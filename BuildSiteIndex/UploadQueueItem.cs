using System;
using System.Diagnostics;

namespace BuildSiteIndex
{
    [Serializable]
    [DebuggerDisplay("UploadType: {UploadType} Path:{Path}, Version: {Version}")]
    public class UploadQueueItem
    {
        public string Path
        {
            get { return Item.Path; }
        }

        public GalleryItem Item { get; set; }

        public UploadType UploadType { get; set; }

        public int Version { get; set; }
    }
}