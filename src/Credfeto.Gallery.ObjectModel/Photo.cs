using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Credfeto.Gallery.ObjectModel
{
    [Serializable]
    [DebuggerDisplay(value: "BasePath: {BasePath}, Image:{ImageExtension} Hash:{PathHash}")]
    public class Photo
    {
        public int Version { get; set; }

        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1056:UriPropertiesShouldNotBeStrings", Justification = "Serialized as string")]
        public string UrlSafePath { get; set; }

        public string BasePath { get; set; }

        public string PathHash { get; set; }

        public string ImageExtension { get; set; }

        public IReadOnlyList<ComponentFile> Files { get; set; }

        public IReadOnlyList<PhotoMetadata> Metadata { get; set; }

        public IReadOnlyList<ImageSize> ImageSizes { get; set; }

        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1056:UriPropertiesShouldNotBeStrings", Justification = "Serialized as string")]
        public string ShortUrl { get; set; }
    }
}