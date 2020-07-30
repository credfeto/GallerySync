using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Credfeto.Gallery.OutputBuilder.Metadata;
using ExifLib;
using TagLib;
using TagLib.Image;
using TagLib.Xmp;
using File = System.IO.File;

namespace Credfeto.Gallery.OutputBuilder
{
    internal static class MetadataExtraction
    {
        public static void AppendMetadata(List<PhotoMetadata> metadata, string name, DateTime value)
        {
            AppendMetadata(metadata: metadata, name: name, FormatDate(value));
        }

        public static void AppendMetadata(List<PhotoMetadata> metadata, string name, int value)
        {
            AppendMetadata(metadata: metadata, name: name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void AppendMetadata(List<PhotoMetadata> metadata, string name, double value)
        {
            AppendMetadata(metadata: metadata, name: name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void AppendMetadata(List<PhotoMetadata> metadata, string name, string value)
        {
            Console.WriteLine(format: " * {0} = {1}", arg0: name, arg1: value);

            if (metadata.All(predicate: candidate => candidate.Name != name))
            {
                metadata.Add(new PhotoMetadata {Name = name, Value = value});
            }
        }

        public static List<PhotoMetadata> ExtractMetadata(Photo sourcePhoto, ISettings settings)
        {
            string rootFolder = settings.RootFolder;

            List<PhotoMetadata> metadata = new List<PhotoMetadata>();

            ExtractXmpMetadata(sourcePhoto: sourcePhoto, metadata: metadata, rootFolder: rootFolder);

            foreach (ComponentFile extension in sourcePhoto.Files.Where(predicate: candidate => !IsXmp(candidate)))
            {
                string filename = Path.Combine(path1: rootFolder, sourcePhoto.BasePath + extension.Extension);

                if (SupportsXmp(extension.Extension))
                {
                    ExtractMetadataFromXmp(metadata: metadata, fileName: filename);
                }

                if (SupportsExif(extension.Extension))
                {
                    ExtractMetadataFromImage(metadata: metadata, fileName: filename);
                }
            }

            return metadata;
        }

        public static void ExtractMetadataFromImage(List<PhotoMetadata> metadata, string fileName)
        {
            TryIgnore(action: () =>
                              {
                                  ExifReader reader = new ExifReader(fileName);

                                  TryIgnore(action: () => ExtractXmpDateTime(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpExposureTime(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpFNumber(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpAperture(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpFocalLength(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpGpsLocation(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpIsoSpeed(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpArtist(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpCopyright(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpCameraMake(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpCameraModel(metadata: metadata, reader: reader));

                                  TryIgnore(action: () => ExtractXmpUserComment(metadata: metadata, reader: reader));
                              });
        }

        public static void ExtractMetadataFromXmp(List<PhotoMetadata> metadata, string fileName)
        {
            try
            {
                byte[] data = File.ReadAllBytes(fileName);

                using (MemoryStream ms = new MemoryStream(buffer: data, writable: false))
                {
                    TagLib.File.IFileAbstraction fa = new StreamFileAbstraction(name: fileName, readStream: ms, writeStream: Stream.Null);

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
                            ExtractXmpTagCommon(metadata: metadata, tag: tag);
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
                tag = new XmpTag(data: xmp, file: null);
            }
            catch (Exception)
            {
                return;
            }

            ExtractXmpTagCommon(metadata: metadata, tag: tag);
        }

        public static bool IsXmp(ComponentFile candidate)
        {
            return StringComparer.InvariantCulture.Equals(x: ".xmp", y: candidate.Extension);
        }

        private static double ExtractGpsMetadataDegreesMinutes(string[] parts, char positive, char negative)
        {
            double part1 = Convert.ToDouble(parts[0]);
            double part2 = Convert.ToDouble(parts[1]
                                                .TrimEnd(positive, negative, char.ToLowerInvariant(positive), char.ToLowerInvariant(negative)));

            double baseValue = part1 + part2 / 60.0d;

            if (parts[1]
                .EndsWith(negative.ToString(), comparisonType: StringComparison.OrdinalIgnoreCase))
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
                .EndsWith(negative.ToString(), comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                baseValue = -baseValue;
            }

            return baseValue;
        }

        private static void ExtractXmpAperture(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.ApertureValue, out uint[] aperture))
            {
                double d = MetadataNormalizationFunctions.ToApexValue(MetadataNormalizationFunctions.ToReal(aperture[0], aperture[1]));

                AppendMetadata(metadata: metadata, name: MetadataNames.APERTURE, MetadataFormatting.FormatAperture(d));
            }
        }

        private static void ExtractXmpArtist(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.Artist, out string artist))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.PHOTOGRAPHER, value: artist);
            }
        }

        private static void ExtractXmpCameraMake(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.Make, out string cameraMake))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.CAMERA_MANUFACTURER, value: cameraMake);
            }
        }

        private static void ExtractXmpCameraModel(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.Model, out string cameraModel))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.CAMERA_MODEL, value: cameraModel);
            }
        }

        private static void ExtractXmpCopyright(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.Artist, out string copyright))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.COPYRIGHT, value: copyright);
            }
        }

        private static void ExtractXmpDateTime(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.DateTimeDigitized, out DateTime whenTaken))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.DATE_TAKEN, value: whenTaken);
            }
            else if (reader.GetTagValue(tag: ExifTags.DateTime, result: out whenTaken))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.DATE_TAKEN, value: whenTaken);
            }
            else if (reader.GetTagValue(tag: ExifTags.DateTimeOriginal, result: out whenTaken))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.DATE_TAKEN, value: whenTaken);
            }
        }

        private static void ExtractXmpExposureTime(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.ExposureTime, out uint[] exposureTime))
            {
                double d = MetadataNormalizationFunctions.ToReal(exposureTime[0], exposureTime[1]);

                AppendMetadata(metadata: metadata, name: MetadataNames.EXPOSURE_TIME, MetadataFormatting.FormatExposure(d));
            }
        }

        private static void ExtractXmpFNumber(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.FNumber, out uint[] fNumber))
            {
                double d = MetadataNormalizationFunctions.ClosestFStop(MetadataNormalizationFunctions.ToReal(fNumber[0], fNumber[1]));

                AppendMetadata(metadata: metadata, name: MetadataNames.APERTURE, MetadataFormatting.FormatFNumber(d));
            }
        }

        private static void ExtractXmpFocalLength(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.FocalLength, out uint[] focalLength))
            {
                double d = MetadataNormalizationFunctions.ToReal(focalLength[0], focalLength[1]);

                AppendMetadata(metadata: metadata, name: MetadataNames.FOCAL_LENGTH, MetadataFormatting.FormatFocalLength(d));
            }
        }

        private static void ExtractXmpGpsLocation(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.GPSLatitude, out double[] latitudeComponents) && reader.GetTagValue(tag: ExifTags.GPSLongitude, out double[] longitudeComponents))
            {
                if (!reader.GetTagValue(tag: ExifTags.GPSLatitudeRef, out string lattitudeRef))
                {
                    lattitudeRef = "N";
                }

                if (!reader.GetTagValue(tag: ExifTags.GPSLongitudeRef, out string longitudeRef))
                {
                    longitudeRef = "E";
                }

                double latitude = latitudeComponents[0] + latitudeComponents[1] / 60 + latitudeComponents[2] / 3600;

                if (StringComparer.InvariantCultureIgnoreCase.Equals(x: "S", y: lattitudeRef))
                {
                    latitude = -latitude;
                }

                double longitude = longitudeComponents[0] + longitudeComponents[1] / 60 + longitudeComponents[2] / 3600;

                if (StringComparer.InvariantCultureIgnoreCase.Equals(x: "W", y: longitudeRef))
                {
                    longitude = -longitude;
                }

                AppendMetadata(metadata: metadata, name: MetadataNames.LATITUDE, value: latitude);
                AppendMetadata(metadata: metadata, name: MetadataNames.LONGITUDE, value: longitude);
            }
        }

        private static void ExtractXmpIsoSpeed(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.PhotographicSensitivity, out ushort isoSpeed))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.ISO_SPEED, value: isoSpeed);
            }
        }

        private static void ExtractXmpMetadata(Photo sourcePhoto, List<PhotoMetadata> metadata, string rootFolder)
        {
            ComponentFile xmpFile = sourcePhoto.Files.FirstOrDefault(IsXmp);

            if (xmpFile != null)
            {
                string sidecarFileName = Path.Combine(path1: rootFolder, sourcePhoto.BasePath + xmpFile.Extension);
                ExtractXmpSidecareAlternative(metadata: metadata, sidecarFileName: sidecarFileName);
                ExtractMetadataFromXmpSideCar(metadata: metadata, fileName: sidecarFileName);
            }
            else
            {
                string xmpFileName = Path.Combine(path1: rootFolder, sourcePhoto.BasePath + ".xmp");

                if (File.Exists(xmpFileName))
                {
                    ExtractXmpSidecareAlternative(metadata: metadata, sidecarFileName: xmpFileName);
                    ExtractMetadataFromXmpSideCar(metadata: metadata, fileName: xmpFileName);
                }
            }
        }

        private static void ExtractXmpSidecareAlternative(List<PhotoMetadata> metadata, string sidecarFileName)
        {
            try
            {
                Dictionary<string, string> properties = XmpFile.ExtractProperties(sidecarFileName);

                if (properties.TryGetValue(key: MetadataNames.LATITUDE, out string latStr) && properties.TryGetValue(key: MetadataNames.LONGITUDE, out string lngStr))
                {
                    string[] latParts = latStr.Split(separator: ',');
                    string[] lngParts = lngStr.Split(separator: ',');

                    if (latParts.Length == 3 && lngParts.Length == 3)
                    {
                        // Degrees Minutes Seconds
                        double lat = ExtractGpsMetadataDegreesMinutesSeconds(parts: latParts, positive: 'N', negative: 'S');
                        double lng = ExtractGpsMetadataDegreesMinutesSeconds(parts: lngParts, positive: 'E', negative: 'W');

                        AppendMetadata(metadata: metadata, name: MetadataNames.LATITUDE, value: lat);
                        AppendMetadata(metadata: metadata, name: MetadataNames.LONGITUDE, value: lng);
                    }
                    else if (latParts.Length == 2 && lngParts.Length == 2)
                    {
                        // Degrees Decimal Minutes
                        double lat = ExtractGpsMetadataDegreesMinutes(parts: latParts, positive: 'N', negative: 'S');
                        double lng = ExtractGpsMetadataDegreesMinutes(parts: lngParts, positive: 'E', negative: 'W');

                        AppendMetadata(metadata: metadata, name: MetadataNames.LATITUDE, value: lat);
                        AppendMetadata(metadata: metadata, name: MetadataNames.LONGITUDE, value: lng);
                    }
                }

                foreach (KeyValuePair<string, string> item in properties.Where(predicate: v => !IsLocation(v)))
                {
                    if (!string.IsNullOrWhiteSpace(item.Value))
                    {
                        AppendMetadata(metadata: metadata, name: item.Key, value: item.Value);
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
                AppendMetadata(metadata: metadata, name: MetadataNames.TITLE, value: tag.Title);
            }

            if (!string.IsNullOrWhiteSpace(tag.Comment) && !IsStupidManufacturerComment(tag.Comment))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.COMMENT, value: tag.Comment);
            }

            string keywords = string.Join(separator: ",", value: tag.Keywords);

            if (!string.IsNullOrWhiteSpace(keywords))
            {
                PhotoMetadata existing = metadata.FirstOrDefault(predicate: candidate => candidate.Name == MetadataNames.KEYWORDS);

                if (existing == null)
                {
                    AppendMetadata(metadata: metadata, name: MetadataNames.KEYWORDS, value: keywords);
                }
                else
                {
                    IOrderedEnumerable<string> allKeywords = existing.Value.Replace(oldChar: ';', newChar: ',')
                                                                     .Split(separator: ',')
                                                                     .Concat(tag.Keywords)
                                                                     .Distinct()
                                                                     .OrderBy(keySelector: x => x);
                    keywords = string.Join(separator: ",", values: allKeywords);
                    metadata.Remove(existing);
                }

                AppendMetadata(metadata: metadata, name: MetadataNames.KEYWORDS, value: keywords);
            }

            AppendMetadata(metadata: metadata, name: MetadataNames.RATING, tag.Rating.GetValueOrDefault(defaultValue: 1));

            if (tag.DateTime.HasValue)
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.DATE_TAKEN, value: tag.DateTime.Value);
            }

            AppendMetadata(metadata: metadata, name: MetadataNames.ORIENTATION, tag.Orientation.ToString());

            if (tag.Latitude.HasValue && tag.Longitude.HasValue)
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.LATITUDE, value: tag.Latitude.Value);
                AppendMetadata(metadata: metadata, name: MetadataNames.LONGITUDE, value: tag.Longitude.Value);
            }

            if (tag.Altitude.HasValue)
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.ALTITUDE, value: tag.Altitude.Value);
            }

            if (tag.ExposureTime.HasValue)
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.EXPOSURE_TIME, MetadataFormatting.FormatExposure(d: tag.ExposureTime.Value, bucket: false));
            }

            if (tag.FNumber.HasValue)
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.APERTURE, MetadataFormatting.FormatFNumber(tag.FNumber.Value));
            }

            if (tag.ISOSpeedRatings.HasValue)
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.ISO_SPEED, value: tag.ISOSpeedRatings.Value);
            }

            if (tag.FocalLength.HasValue)
            {
                AppendMetadata(metadata: metadata,
                               name: MetadataNames.FOCAL_LENGTH,
                               MetadataFormatting.FormatFocalLength(d: tag.FocalLength.Value, (int) tag.FocalLengthIn35mmFilm.GetValueOrDefault(defaultValue: 0)));
            }

            if (!string.IsNullOrWhiteSpace(tag.Make))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.CAMERA_MANUFACTURER, value: tag.Make);
            }

            if (!string.IsNullOrWhiteSpace(tag.Model))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.CAMERA_MODEL, value: tag.Model);
            }

            if (!string.IsNullOrWhiteSpace(tag.Creator))
            {
                AppendMetadata(metadata: metadata, name: MetadataNames.PHOTOGRAPHER, value: tag.Creator);
            }
        }

        private static void ExtractXmpUserComment(List<PhotoMetadata> metadata, ExifReader reader)
        {
            if (reader.GetTagValue(tag: ExifTags.UserComment, out string userComment))
            {
                if (!string.IsNullOrWhiteSpace(userComment) && !IsStupidManufacturerComment(userComment))
                {
                    AppendMetadata(metadata: metadata, name: MetadataNames.COMMENT, value: userComment);
                }
            }
        }

        private static string FormatDate(DateTime value)
        {
            return value.ToString(format: "s");
        }

        private static bool IsLocation(KeyValuePair<string, string> keyValuePair)
        {
            string[] p = {MetadataNames.LATITUDE, MetadataNames.LONGITUDE};

            return p.Any(predicate: v => StringComparer.InvariantCultureIgnoreCase.Equals(x: v, y: keyValuePair.Key));
        }

        private static bool IsStupidManufacturerComment(string userComment)
        {
            // Why companies put this crap in the metadata when they already set the model and manufacturer I've no idea.
            string[] badComments = {"GE", "OLYMPUS DIGITAL CAMERA", "Minolta DSC"};

            return badComments.Any(predicate: text => StringComparer.InvariantCultureIgnoreCase.Equals(x: text, userComment.Trim()));
        }

        private static bool SupportsExif(string extension)
        {
            string[] supportedExtensions = {".jpg", ".jpeg", ".jpe", ".gif", ".tiff", ".tif"};

            return supportedExtensions.Any(predicate: ext => StringComparer.InvariantCultureIgnoreCase.Equals(x: ext, y: extension));
        }

        private static bool SupportsXmp(string extension)
        {
            string[] supportedExtensions = {"arw", "cf2", "cr2", "crw", "dng", "erf", "mef", "mrw", "nef", "orf", "pef", "raf", "raw", "rw2", "sr2", "x3f"};

            return supportedExtensions.Any(predicate: ext => StringComparer.InvariantCultureIgnoreCase.Equals(x: ext, y: extension));
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