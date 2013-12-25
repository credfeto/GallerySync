using System;
using System.Collections.Generic;

namespace BuildSiteIndex
{
    [Serializable]
    public class GalleryEntry
    {
        public string Title { get; set; }

        public List<GalleryEntry> Children { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateUpdated { get; set; }
    }
}