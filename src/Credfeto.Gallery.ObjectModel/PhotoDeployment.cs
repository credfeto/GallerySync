using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Credfeto.Gallery.ObjectModel;

[Serializable]
public sealed class PhotoDeployment
{
    [SuppressMessage("Meziantou.Analyzer", "MA0016: Use Collection Abstraction", Justification = "For Compatibility")]
    public Dictionary<string, Photo> Photos { get; } = new(StringComparer.Ordinal);
}