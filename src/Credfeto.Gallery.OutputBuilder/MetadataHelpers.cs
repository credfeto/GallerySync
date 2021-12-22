using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;

namespace Credfeto.Gallery.OutputBuilder;

internal static class MetadataHelpers
{
    public static DateTime ExtractCreationDate(IReadOnlyList<PhotoMetadata> metadata)
    {
        PhotoMetadata dateTaken = metadata.FirstOrDefault(predicate: candidate => candidate.Name == MetadataNames.DATE_TAKEN);

        if (dateTaken == null)
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParse(s: dateTaken.Value, out DateTime value))
        {
            return value;
        }

        return DateTime.MinValue;
    }
}