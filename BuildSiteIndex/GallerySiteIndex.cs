﻿using System;
using System.Collections.Generic;

namespace BuildSiteIndex
{
    [Serializable]
    public sealed class GallerySiteIndex
    {
        public int version { get; set; }

        public List<GalleryItem> items { get; set; }

        public List<string> deletedItems { get; set; }
    }
}