using System;
using System.Collections.Generic;
using System.Linq;
using FileNaming;
using ObjectModel;

namespace OutputBuilderClient
{
    internal static class MetadataHelpers
    {
        public static DateTime ExtractCreationDate(List<PhotoMetadata> metadata)
        {
            PhotoMetadata dateTaken = metadata.FirstOrDefault(predicate: candidate => candidate.Name == MetadataNames.DateTaken);

            if (dateTaken == null)
            {
                return DateTime.MinValue;
            }

            if (DateTime.TryParse(dateTaken.Value, out DateTime value))
            {
                return value;
            }

            return DateTime.MinValue;
        }
    }
}