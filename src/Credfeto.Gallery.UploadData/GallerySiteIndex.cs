using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Credfeto.Gallery.UploadData
{
    [Serializable]
    public sealed class GallerySiteIndex
    {
        public int Version { get; set; }

        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1002:DoNotExposeGenericLists", Justification = "Existing API")]
        public List<GalleryItem> Items { get; set; }

        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1002:DoNotExposeGenericLists", Justification = "Existing API")]
        public List<string> DeletedItems { get; set; }
    }
}