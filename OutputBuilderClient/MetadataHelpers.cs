using System;
using System.Collections.Generic;
using System.Linq;
using FileNaming;
using ObjectModel;

namespace OutputBuilderClient
{
    static internal class MetadataHelpers
    {
        public static DateTime ExtractCreationDate(List<PhotoMetadata> metadata)
        {
            var dateTaken = metadata.FirstOrDefault(candidate => candidate.Name == MetadataNames.DateTaken);
            if (dateTaken == null)
                return DateTime.MinValue;

            DateTime value;
            if (DateTime.TryParse(dateTaken.Value, out value))
                return value;

            return DateTime.MinValue;
        }
    }
}