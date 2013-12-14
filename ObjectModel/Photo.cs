﻿using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Twaddle.Gallery.ObjectModel
{
    [Serializable]
    [DebuggerDisplay("BasePath: {BasePath}, Image:{ImageExtension} Hash:{PathHash}")]
    public class Photo
    {
        public string UrlSafePath { get; set; }

        public string BasePath { get; set; }

        public string PathHash { get; set; }

        public string ImageExtension { get; set; }

        public List<ComponentFile> Files { get; set; }

        public List<PhotoMetadata> Metadata { get; set; }

        public List<ImageSize> ImageSizes { get; set; }
    }

    [Serializable]
    [DebuggerDisplay("Width: {Width}, Height:{Height}")]
    public class ImageSize
    {
        int Width { get; set; }
        int Height { get; set; }
    }
}