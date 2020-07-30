using System;
using System.Collections.Generic;

namespace Credfeto.Gallery.UploadData
{
    [Serializable]
    public sealed class GallerySiteIndex
    {
        public int Version { get; set; }

        public List<GalleryItem> Items { get; set; }

        public List<string> DeletedItems { get; set; }
    }
}