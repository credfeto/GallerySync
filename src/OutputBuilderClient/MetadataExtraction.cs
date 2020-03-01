using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ExifLib;
using FileNaming;
using ObjectModel;
using OutputBuilderClient.Metadata;
using TagLib;
using TagLib.Image;
using TagLib.Xmp;
using File = System.IO.File;

namespace OutputBuilderClient
{
    internal static class MetadataExtraction
    {
        public static void AppendMetadata(List<PhotoMetadata> metadata, string name, DateTime value)
        {
            AppendMetadata(metadata, name, FormatDate(value));
        }

        public static void AppendMetadata(List<PhotoMetadata> metadata, string name, int value)
        {
            AppendMetadata(metadata, name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void AppendMetadata(List<PhotoMetadata> metadata, string name, double value)
        {
            AppendMetadata(metadata, name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void AppendMetadata(List<PhotoMetadata> metadata, string name, string value)
        {
            Console.WriteLine(format: " * {0} = {1}", name, value);

            if (metadata.All(predicate: candidate => candidate.Name != name))
            {
                metadata.Add(new PhotoMetadata {Name = name, Value = value});
            }
        }

        public static List<PhotoMetadata> ExtractMetadata(Photo sourcePhoto)
        {
            string rootFolder = Settings.RootFolder;

            List<PhotoMetadata> metadata = new List<PhotoMetadata>();

            ExtractXmpMetadata(sourcePhoto, metadata, rootFolder);

            foreach (ComponentFile extension in sourcePhoto.Files.Where(predicate: candidate => !IsXmp(candidate)))
            {
                string filename = Path.Combine(rootFolder, sourcePhoto.BasePath + extension.Extension);

                if (SupportsXmp(extension.Extension))
                {
                    ExtractMetadataFromXmp(metadata, filename);
                }

                if (SupportsExif(extension.Extension))
                {
                    ExtractMetadataFromImage(metadata, filename);
                }
            }

            return metadata;
        }

        public static void ExtractMetadataFromImage(List<PhotoMetadata> metadata, string fileName)
        {
            TryIgnore(action: () =>
            {
                ExifReader reader = new ExifReader(fileName);

                TryIgnore(action: () => ExtractXmpDateTime(metadata, reader));

                TryIgnore(action: () => ExtractXmpExposureTime(metadata, reader));

                TryIgnore(action: () => ExtractXmpFNumber(metadata, reader));

                TryIgnore(action: () => ExtractXmpAperture(metadata, reader));

                TryIgnore(action: () => ExtractXmpFocalLength(metadata, reader));

                TryIgnore(action: () => ExtractXmpGpsLocation(metadata, reader));

                TryIgnore(action: () => ExtractXmpIsoSpeed(metadata, reader));

                TryIgnore(action: () => ExtractXmpArtist(metadata, reader));

                TryIgnore(action: () => ExtractXmpCopyright(metadata, reader));

                TryIgnore(action: () => ExtractXmpCameraMake(metadata, reader));

                TryIgnore(action: () => ExtractXmpCameraModel(metadata, reader));

                TryIgnore(action: () => ExtractXmpUserComment(metadata, reader));
            });
        }

        public static void ExtractMetadataFromXmp(List<PhotoMetadata> metadata, string fileName)
        {
            try
            {
                byte[] data = File.ReadAllBytes(fileName);

                using (MemoryStream ms = new MemoryStream(data, writable: false))
                {
                    TagLib.File.IFileAbstraction fa = new StreamFileAbstraction(fileName, ms, Stream.Null);

                    using (TagLib.File tlf = TagLib.File.Create(fa))
                    {
                        TagLib.Image.File file = tlf as TagLib.Image.File;

                        if (file == null)
                        {
                            return;
                        }

                        ImageTag tag = file.GetTag(TagTypes.XMP) as ImageTag;

                        if (tag != null && !tag.IsEmpty)
                        {
                            ExtractXmpTagCommon(metadata, tag);
                        }
                    }
                }
            }
            catch
            {
                // Don't care'
            }
        }

        public static void ExtractMetadataFromXmpSideCar(List<PhotoMetadata> metadata, string fileName)
        {
            string xmp = File.ReadAllText(fileName);

            XmpTag tag = null;

            try
            {
                tag = new XmpTag(xmp, file: null);
            }
            catch (Exception)
            {
                return;
            }

            ExtractXmpTagCommon(metadata, tag);
        }

        public static bool IsXmp(ComponentFile candidate)
        {
            return StringComparer.InvariantCulture.Equals(x: ".xmp", candidate.Extension);
        }

        private static double ExtractGpsMetadataDegreesMinutes(string[] parts, char positive, char negative)
        {
            double part1 = Convert.ToDouble(parts[0]);
            double part2 = Convert.ToDouble(parts[1]
                                                .TrimEnd(positive, negative, char.ToLowerInvariant(positive), char.ToLowerInvariant(negative)));

            double baseValue = part1 + part2 / 60.0d;

            if (parts[1]
                .EndsWith(negative.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                baseValue = -baseValue;
            }

            return baseValue;
        }

        private static double ExtractGpsMetadataDegreesMinutesSeconds(string[] parts, char positive, char negative)
        {
            double part1 = Convert.ToDouble(parts[0]);
            double part2 = Convert.ToDouble(parts[1]);
            double part3 = Convert.ToDouble(parts[2]
                                                .TrimEnd(positive, negative, char.ToLowerInvariant(positive), char.ToLowerInvariant(negative)));

            double baseValue = part1 + part2 / 60d + part3 / 3600d;

            if (parts[2]
                .EndsWith(negative.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                baseValue = -baseValue;
            }

            return baseValue;
        }

        private static void ExtractXmpAperture(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.ApertureValue, out uint[] aperture))
            {
                double d = MetadataNormalizationFunctions.ToApexValue(MetadataNormalizationFunctions.ToReal(aperture[0], aperture[1]));

                AppendMetadata(metadata, MetadataNames.Aperture, MetadataFormatting.FormatAperture(d));
            }
        }

        private static void ExtractXmpArtist(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.Artist, out string artist))
            {
                AppendMetadata(metadata, MetadataNames.Photographer, artist);
            }
        }

        private static void ExtractXmpCameraMake(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.Make, out string cameraMake))
            {
                AppendMetadata(metadata, MetadataNames.CameraManufacturer, cameraMake);
            }
        }

        private static void ExtractXmpCameraModel(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.Model, out string cameraModel))
            {
                AppendMetadata(metadata, MetadataNames.CameraModel, cameraModel);
            }
        }

        private static void ExtractXmpCopyright(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.Artist, out string copyright))
            {
                AppendMetadata(metadata, MetadataNames.Copyright, copyright);
            }
        }

        private static void ExtractXmpDateTime(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.DateTimeDigitized, out DateTime whenTaken))
            {
                AppendMetadata(metadata, MetadataNames.DateTaken, whenTaken);
            }
            else if (reader.GetTagValue(ExifTags.DateTime, out whenTaken))
            {
                AppendMetadata(metadata, MetadataNames.DateTaken, whenTaken);
            }
            else if (reader.GetTagValue(ExifTags.DateTimeOriginal, out whenTaken))
            {
                AppendMetadata(metadata, MetadataNames.DateTaken, whenTaken);
            }
        }

        private static void ExtractXmpExposureTime(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.ExposureTime, out uint[] exposureTime))
            {
                double d = MetadataNormalizationFunctions.ToReal(exposureTime[0], exposureTime[1]);

                AppendMetadata(metadata, MetadataNames.ExposureTime, MetadataFormatting.FormatExposure(d));
            }
        }

        private static void ExtractXmpFNumber(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.FNumber, out uint[] fNumber))
            {
                double d = MetadataNormalizationFunctions.ClosestFStop(MetadataNormalizationFunctions.ToReal(fNumber[0], fNumber[1]));

                AppendMetadata(metadata, MetadataNames.Aperture, MetadataFormatting.FormatFNumber(d));
            }
        }

        private static void ExtractXmpFocalLength(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.FocalLength, out uint[] focalLength))
            {
                double d = MetadataNormalizationFunctions.ToReal(focalLength[0], focalLength[1]);

                AppendMetadata(metadata, MetadataNames.FocalLength, MetadataFormatting.FormatFocalLength(d));
            }
        }

        private static void ExtractXmpGpsLocation(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.GPSLatitude, out double[] latitudeComponents) && reader.GetTagValue(ExifTags.GPSLongitude, out double[] longitudeComponents))
            {
                if (!reader.GetTagValue(ExifTags.GPSLatitudeRef, out string lattitudeRef))
                {
                    lattitudeRef = "N";
                }

                if (!reader.GetTagValue(ExifTags.GPSLongitudeRef, out string longitudeRef))
                {
                    longitudeRef = "E";
                }

                double latitude = latitudeComponents[0] + latitudeComponents[1] / 60 + latitudeComponents[2] / 3600;

                if (StringComparer.InvariantCultureIgnoreCase.Equals(x: "S", lattitudeRef))
                {
                    latitude = -latitude;
                }

                double longitude = longitudeComponents[0] + longitudeComponents[1] / 60 + longitudeComponents[2] / 3600;

                if (StringComparer.InvariantCultureIgnoreCase.Equals(x: "W", longitudeRef))
                {
                    longitude = -longitude;
                }

                AppendMetadata(metadata, MetadataNames.Latitude, latitude);
                AppendMetadata(metadata, MetadataNames.Longitude, longitude);
            }
        }

        private static void ExtractXmpIsoSpeed(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.PhotographicSensitivity, out ushort isoSpeed))
            {
                AppendMetadata(metadata, MetadataNames.IsoSpeed, isoSpeed);
            }
        }

        private static void ExtractXmpMetadata(Photo sourcePhoto, List<PhotoMetadata> metadata, string rootFolder)
        {
            ComponentFile xmpFile = sourcePhoto.Files.FirstOrDefault(IsXmp);

            if (xmpFile != null)
            {
                string sidecarFileName = Path.Combine(rootFolder, sourcePhoto.BasePath + xmpFile.Extension);
                ExtractXmpSidecareAlternative(metadata, sidecarFileName);
                ExtractMetadataFromXmpSideCar(metadata, sidecarFileName);
            }
            else
            {
                string xmpFileName = Path.Combine(rootFolder, sourcePhoto.BasePath + ".xmp");

                if (File.Exists(xmpFileName))
                {
                    ExtractXmpSidecareAlternative(metadata, xmpFileName);
                    ExtractMetadataFromXmpSideCar(metadata, xmpFileName);
                }
            }
        }

        private static void ExtractXmpSidecareAlternative(List<PhotoMetadata> metadata, string sidecarFileName)
        {
            try
            {
                Dictionary<string, string> properties = XmpFile.ExtractProperties(sidecarFileName);

                if (properties.TryGetValue(MetadataNames.Latitude, out string latStr) && properties.TryGetValue(MetadataNames.Longitude, out string lngStr))
                {
                    string[] latParts = latStr.Split(separator: ',');
                    string[] lngParts = lngStr.Split(separator: ',');

                    if (latParts.Length == 3 && lngParts.Length == 3)
                    {
                        // Degrees Minutes Seconds
                        double lat = ExtractGpsMetadataDegreesMinutesSeconds(latParts, positive: 'N', negative: 'S');
                        double lng = ExtractGpsMetadataDegreesMinutesSeconds(lngParts, positive: 'E', negative: 'W');

                        AppendMetadata(metadata, MetadataNames.Latitude, lat);
                        AppendMetadata(metadata, MetadataNames.Longitude, lng);
                    }
                    else if (latParts.Length == 2 && lngParts.Length == 2)
                    {
                        // Degrees Decimal Minutes
                        double lat = ExtractGpsMetadataDegreesMinutes(latParts, positive: 'N', negative: 'S');
                        double lng = ExtractGpsMetadataDegreesMinutes(lngParts, positive: 'E', negative: 'W');

                        AppendMetadata(metadata, MetadataNames.Latitude, lat);
                        AppendMetadata(metadata, MetadataNames.Longitude, lng);
                    }
                }

                foreach (KeyValuePair<string, string> item in properties.Where(predicate: v => !IsLocation(v)))
                {
                    if (!string.IsNullOrWhiteSpace(item.Value))
                    {
                        AppendMetadata(metadata, item.Key, item.Value);
                    }
                }
            }
            catch
            {
                // Don't care
            }
        }

        private static void ExtractXmpTagCommon(List<PhotoMetadata> metadata, ImageTag tag)
        {
            if (!string.IsNullOrWhiteSpace(tag.Title))
            {
                AppendMetadata(metadata, MetadataNames.Title, tag.Title);
            }

            if (!string.IsNullOrWhiteSpace(tag.Comment) && !IsStupidManufacturerComment(tag.Comment))
            {
                AppendMetadata(metadata, MetadataNames.Comment, tag.Comment);
            }

            string keywords = string.Join(separator: ",", tag.Keywords);

            if (!string.IsNullOrWhiteSpace(keywords))
            {
                PhotoMetadata existing = metadata.FirstOrDefault(predicate: candidate => candidate.Name == MetadataNames.Keywords);

                if (existing == null)
                {
                    AppendMetadata(metadata, MetadataNames.Keywords, keywords);
                }
                else
                {
                    IOrderedEnumerable<string> allKeywords = existing.Value.Replace(oldChar: ';', newChar: ',')
                        .Split(separator: ',')
                        .Concat(tag.Keywords)
                        .Distinct()
                        .OrderBy(keySelector: x => x);
                    keywords = string.Join(separator: ",", allKeywords);
                    metadata.Remove(existing);
                }

                AppendMetadata(metadata, MetadataNames.Keywords, keywords);
            }

            AppendMetadata(metadata, MetadataNames.Rating, tag.Rating.GetValueOrDefault(defaultValue: 1));

            if (tag.DateTime.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.DateTaken, tag.DateTime.Value);
            }

            AppendMetadata(metadata, MetadataNames.Orientation, tag.Orientation.ToString());

            if (tag.Latitude.HasValue && tag.Longitude.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.Latitude, tag.Latitude.Value);
                AppendMetadata(metadata, MetadataNames.Longitude, tag.Longitude.Value);
            }

            if (tag.Altitude.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.Altitude, tag.Altitude.Value);
            }

            if (tag.ExposureTime.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.ExposureTime, MetadataFormatting.FormatExposure(tag.ExposureTime.Value, bucket: false));
            }

            if (tag.FNumber.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.Aperture, MetadataFormatting.FormatFNumber(tag.FNumber.Value));
            }

            if (tag.ISOSpeedRatings.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.IsoSpeed, tag.ISOSpeedRatings.Value);
            }

            if (tag.FocalLength.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.FocalLength, MetadataFormatting.FormatFocalLength(tag.FocalLength.Value, (int) tag.FocalLengthIn35mmFilm.GetValueOrDefault(defaultValue: 0)));
            }

            if (!string.IsNullOrWhiteSpace(tag.Make))
            {
                AppendMetadata(metadata, MetadataNames.CameraManufacturer, tag.Make);
            }

            if (!string.IsNullOrWhiteSpace(tag.Model))
            {
                AppendMetadata(metadata, MetadataNames.CameraModel, tag.Model);
            }

            if (!string.IsNullOrWhiteSpace(tag.Creator))
            {
                AppendMetadata(metadata, MetadataNames.Photographer, tag.Creator);
            }
        }

        private static void ExtractXmpUserComment(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(ExifTags.UserComment, out string userComment))
            {
                if (!string.IsNullOrWhiteSpace(userComment) && !IsStupidManufacturerComment(userComment))
                {
                    AppendMetadata(metadata, MetadataNames.Comment, userComment);
                }
            }
        }

        private static string FormatDate(DateTime value)
        {
            return value.ToString(format: "s");
        }

        private static bool IsLocation(KeyValuePair<string, string> keyValuePair)
        {
            string[] p = {MetadataNames.Latitude, MetadataNames.Longitude};

            return p.Any(predicate: v => StringComparer.InvariantCultureIgnoreCase.Equals(v, keyValuePair.Key));
        }

        private static bool IsStupidManufacturerComment(string userComment)
        {
            // Why companies put this crap in the metadata when they already set the model and manufacturer I've no idea.
            string[] badComments = {"GE", "OLYMPUS DIGITAL CAMERA", "Minolta DSC"};

            return badComments.Any(predicate: text => StringComparer.InvariantCultureIgnoreCase.Equals(text, userComment.Trim()));
        }

        private static bool SupportsExif(string extension)
        {
            string[] supportedExtensions = {".jpg", ".jpeg", ".jpe", ".gif", ".tiff", ".tif"};

            return supportedExtensions.Any(predicate: ext => StringComparer.InvariantCultureIgnoreCase.Equals(ext, extension));
        }

        private static bool SupportsXmp(string extension)
        {
            string[] supportedExtensions = {"arw", "cf2", "cr2", "crw", "dng", "erf", "mef", "mrw", "nef", "orf", "pef", "raf", "raw", "rw2", "sr2", "x3f"};

            return supportedExtensions.Any(predicate: ext => StringComparer.InvariantCultureIgnoreCase.Equals(ext, extension));
        }

        private static void TryIgnore(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                // Don't care
            }
        }
    }
}