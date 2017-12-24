using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ObjectModel
{
    [Serializable]
    [DebuggerDisplay("BasePath: {BasePath}, Image:{ImageExtension} Hash:{PathHash}")]
    public class Photo
    {
        public int Version { get; set; }

        public string UrlSafePath { get; set; }

        public string BasePath { get; set; }

        public string PathHash { get; set; }

        public string ImageExtension { get; set; }

        public List<ComponentFile> Files { get; set; }

        public List<PhotoMetadata> Metadata { get; set; }

        public List<ImageSize> ImageSizes { get; set; }

        public string ShortUrl { get; set; }
    }
}