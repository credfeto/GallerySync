using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Credfeto.Gallery.ObjectModel;

[Serializable]
[DebuggerDisplay(value: "BasePath: {BasePath}, Image:{ImageExtension} Hash:{PathHash}")]
public sealed class Photo
{
    public int Version { get; set; }

    [SuppressMessage(category: "Microsoft.Design", checkId: "CA1056:UriPropertiesShouldNotBeStrings", Justification = "Serialized as string")]
    public string UrlSafePath { get; set; } = default!;

    public string BasePath { get; set; } = default!;

    public string PathHash { get; set; } = default!;

    public string ImageExtension { get; set; } = default!;

    public IReadOnlyList<ComponentFile> Files { get; set; } = default!;

    public IReadOnlyList<PhotoMetadata> Metadata { get; set; } = default!;

    public IReadOnlyList<ImageSize> ImageSizes { get; set; } = default!;

    [SuppressMessage(category: "Microsoft.Design", checkId: "CA1056:UriPropertiesShouldNotBeStrings", Justification = "Serialized as string")]
    public string ShortUrl { get; set; } = default!;
}