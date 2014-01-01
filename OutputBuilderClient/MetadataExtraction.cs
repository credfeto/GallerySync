﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ExifLib;
using FileNaming;
using OutputBuilderClient.Metadata;
using OutputBuilderClient.Properties;
using TagLib;
using TagLib.Image;
using TagLib.Xmp;
using Twaddle.Gallery.ObjectModel;
using File = System.IO.File;

namespace OutputBuilderClient
{
    internal static class MetadataExtraction
    {
        public static List<PhotoMetadata> ExtractMetadata(Photo sourcePhoto)
        {
            string rootFolder = Settings.Default.RootFolder;


            var metadata = new List<PhotoMetadata>();

            ExtractXmpMetadata(sourcePhoto, metadata, rootFolder);

            foreach (
                ComponentFile extension in
                    sourcePhoto.Files.Where(
                        candidate => !IsXmp(candidate)))
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

        private static bool SupportsExif(string extension)
        {
            var supportedExtensions = new[]
                {
                    ".jpg",
                    ".jpeg",
                    ".jpe",
                    ".gif",
                    ".tiff",
                    ".tif"
                };

            return supportedExtensions.Any(ext => StringComparer.InvariantCultureIgnoreCase.Equals(ext, extension));
        }

        private static bool SupportsXmp(string extension)
        {
            var supportedExtensions = new[]
                {
                    "arw",
                    "cf2",
                    "cr2",
                    "crw",
                    "dng",
                    "erf",
                    "mef",
                    "mrw",
                    "nef",
                    "orf",
                    "pef",
                    "raf",
                    "raw",
                    "rw2",
                    "sr2",
                    "x3f"
                };

            return supportedExtensions.Any(ext => StringComparer.InvariantCultureIgnoreCase.Equals(ext, extension));
        }

        private static bool ExtractXmpMetadata(Photo sourcePhoto, List<PhotoMetadata> metadata, string rootFolder)
        {
            ComponentFile xmpFile =
                sourcePhoto.Files.FirstOrDefault(
                    IsXmp);

            if (xmpFile != null)
            {
                string sidecarFileName = Path.Combine(rootFolder, sourcePhoto.BasePath + xmpFile.Extension);
                ExtractXmpSidecareAlternative(metadata, sidecarFileName);
                ExtractMetadataFromXmpSideCar(metadata, sidecarFileName);
                if (metadata.Any())
                {
                    return true;
                }
            }
            else
            {
                string xmpFileName = Path.Combine(rootFolder, sourcePhoto.BasePath + ".xmp");
                if (File.Exists(xmpFileName))
                {
                    ExtractXmpSidecareAlternative(metadata, xmpFileName);
                    ExtractMetadataFromXmpSideCar(metadata, xmpFileName);
                    if (metadata.Any())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ExtractXmpSidecareAlternative(List<PhotoMetadata> metadata, string sidecarFileName)
        {
            try
            {
                Dictionary<string, string> properties = XmpFile.ExtractProperties(sidecarFileName);

                string latStr;
                string lngStr;
                if (properties.TryGetValue(MetadataNames.Latitude, out latStr) &&
                    properties.TryGetValue(MetadataNames.Longitude, out lngStr))
                {
                    string[] latParts = latStr.Split(',');
                    string[] lngParts = lngStr.Split(',');

                    if (latParts.Length == 3 && lngParts.Length == 3)
                    {
                        double lat = ExtractGpsMetadata(latParts, 'N', 'S');
                        double lng = ExtractGpsMetadata(lngParts, 'E', 'W');

                        AppendMetadata(metadata, MetadataNames.Latitude, lat);
                        AppendMetadata(metadata, MetadataNames.Longitude, lng);
                    }
                }


                foreach (var item in properties.Where(v => !IsLocation(v)))
                {
                    if (!string.IsNullOrWhiteSpace(item.Value))
                    {
                        AppendMetadata(metadata, item.Key, item.Value);
                    }
                }
            }
            catch
            {
            }
        }

        private static double ExtractGpsMetadata(string[] parts, char positive, char negative)
        {
            double part1 = Convert.ToDouble(parts[0]);
            double part2 = Convert.ToDouble(parts[1]);
            double part3 =
                Convert.ToDouble(parts[2].TrimEnd(positive, negative, char.ToLowerInvariant(positive),
                                                  char.ToLowerInvariant(negative)));

            double baseValue = part1 + part2/60 + part3/3600;
            if (parts[2].EndsWith(negative.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                baseValue = -baseValue;
            }

            return baseValue;
        }

        private static bool IsLocation(KeyValuePair<string, string> keyValuePair)
        {
            var p = new[] {MetadataNames.Latitude, MetadataNames.Longitude};

            return p.Any(v => StringComparer.InvariantCultureIgnoreCase.Equals(v, keyValuePair.Key));
        }


        public static bool IsXmp(ComponentFile candidate)
        {
            return StringComparer.InvariantCulture.Equals(".xmp", candidate.Extension);
        }

        public static void ExtractMetadataFromXmpSideCar(List<PhotoMetadata> metadata, string fileName)
        {
            string xmp = File.ReadAllText(fileName);

            XmpTag tag = null;
            try
            {
                tag = new XmpTag(xmp, null);
            }
            catch (Exception)
            {
                return;
            }

            ExtractXmpTagCommon(metadata, tag);
        }

        public static void ExtractMetadataFromXmp(List<PhotoMetadata> metadata, string fileName)
        {
            try
            {
                var file = TagLib.File.Create(fileName) as TagLib.Image.File;
                if (file == null)
                {
                    return;
                }

                var tag = file.GetTag(TagTypes.XMP) as ImageTag;
                if (tag != null && !tag.IsEmpty)
                {
                    ExtractXmpTagCommon(metadata, tag);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void ExtractXmpTagCommon(List<PhotoMetadata> metadata, ImageTag tag)
        {
            if (!String.IsNullOrWhiteSpace(tag.Title))
            {
                AppendMetadata(metadata, MetadataNames.Title, tag.Title);
            }
            if (!String.IsNullOrWhiteSpace(tag.Comment) && !IsStupidManufacturerComment(tag.Comment))
            {
                AppendMetadata(metadata, MetadataNames.Comment, tag.Comment);
            }
            string keywords = String.Join(",", tag.Keywords);
            if (!String.IsNullOrWhiteSpace(keywords))
            {
                AppendMetadata(metadata, MetadataNames.Keywords, keywords);
            }

            AppendMetadata(metadata, MetadataNames.Rating, tag.Rating.GetValueOrDefault(1));

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
                AppendMetadata(metadata, MetadataNames.ExposureTime, tag.ExposureTime.Value);
            }
            if (tag.FNumber.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.FNumber, String.Format("F/{0}", tag.FNumber.Value));
            }
            if (tag.ISOSpeedRatings.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.ISOSpeed, tag.ISOSpeedRatings.Value);
            }
            if (tag.FocalLength.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.FocalLength, tag.FocalLength.Value);
            }
            else if (tag.FocalLengthIn35mmFilm.HasValue)
            {
                AppendMetadata(metadata, MetadataNames.FocalLength, tag.FocalLengthIn35mmFilm.Value);
            }
            if (!String.IsNullOrWhiteSpace(tag.Make))
            {
                AppendMetadata(metadata, MetadataNames.CameraManufacturer, tag.Make);
            }
            if (!String.IsNullOrWhiteSpace(tag.Model))
            {
                AppendMetadata(metadata, MetadataNames.CameraModel, tag.Model);
            }

            if (!String.IsNullOrWhiteSpace(tag.Creator))
            {
                AppendMetadata(metadata, MetadataNames.Photographer, tag.Creator);
            }
        }

        public static void ExtractMetadataFromImage(List<PhotoMetadata> metadata, string fileName)
        {
            TryIgnore(() =>
                {
                    var reader = new ExifReader(fileName);

                    TryIgnore(() => ExtractXmpDateTime(metadata, reader));

                    TryIgnore(() => ExtractXmpExposureTime(metadata, reader));

                    TryIgnore(() => ExtractXmpFNumber(metadata, reader));

                    TryIgnore(() => ExtractXmpAperture(metadata, reader));

                    TryIgnore(() => ExtractXmpFocalLength(metadata, reader));

                    TryIgnore(() => ExtractXmpGpsLocation(metadata, reader));

                    TryIgnore(() => ExtractXmpIsoSpeed(metadata, reader));

                    TryIgnore(() => ExtractXmpArtist(metadata, reader));

                    TryIgnore(() => ExtractXmpCopyright(metadata, reader));

                    TryIgnore(() => ExtractXmpCameraMake(metadata, reader));

                    TryIgnore(() => ExtractXmpCameraModel(metadata, reader));

                    TryIgnore(() => ExtractXmpUserComment(metadata, reader));
                }
                );
        }

        private static void ExtractXmpUserComment(List<PhotoMetadata> metadata, ExifReader reader)
        {
            string userComment;
            if (reader.GetTagValue(ExifTags.UserComment, out userComment))
            {
                if (!string.IsNullOrWhiteSpace(userComment) && !IsStupidManufacturerComment(userComment))
                {
                    AppendMetadata(metadata, MetadataNames.Comment, userComment);
                }
            }
        }

        private static void ExtractXmpCameraModel(List<PhotoMetadata> metadata, ExifReader reader)
        {
            string cameraModel;
            if (reader.GetTagValue(ExifTags.Model, out cameraModel))
            {
                AppendMetadata(metadata, MetadataNames.CameraModel, cameraModel);
            }
        }

        private static void ExtractXmpCameraMake(List<PhotoMetadata> metadata, ExifReader reader)
        {
            string cameraMake;
            if (reader.GetTagValue(ExifTags.Make, out cameraMake))
            {
                AppendMetadata(metadata, MetadataNames.CameraManufacturer, cameraMake);
            }
        }

        private static void ExtractXmpCopyright(List<PhotoMetadata> metadata, ExifReader reader)
        {
            string copyright;
            if (reader.GetTagValue(ExifTags.Artist, out copyright))
            {
                AppendMetadata(metadata, MetadataNames.Copyright, copyright);
            }
        }

        private static void ExtractXmpArtist(List<PhotoMetadata> metadata, ExifReader reader)
        {
            string artist;
            if (reader.GetTagValue(ExifTags.Artist, out artist))
            {
                AppendMetadata(metadata, MetadataNames.Photographer, artist);
            }
        }

        private static void ExtractXmpIsoSpeed(List<PhotoMetadata> metadata, ExifReader reader)
        {
            UInt16 isoSpeed;
            if (reader.GetTagValue(ExifTags.ISOSpeedRatings, out isoSpeed) )
            {
                AppendMetadata(metadata, MetadataNames.ISOSpeed, isoSpeed);
            }
        }

        private static void ExtractXmpGpsLocation(List<PhotoMetadata> metadata, ExifReader reader)
        {
            double[] latitudeComponents;
            double[] longitudeComponents;
            if (reader.GetTagValue(ExifTags.GPSLatitude, out latitudeComponents) &&
                reader.GetTagValue(ExifTags.GPSLongitude, out longitudeComponents)
                )
            {
                string lattitudeRef;
                if (!reader.GetTagValue(ExifTags.GPSLatitudeRef, out lattitudeRef))
                {
                    lattitudeRef = "N";
                }

                string longitudeRef;
                if (!reader.GetTagValue(ExifTags.GPSLongitudeRef, out longitudeRef))
                {
                    longitudeRef = "E";
                }

                double latitude = latitudeComponents[0] + latitudeComponents[1]/60 +
                                  latitudeComponents[2]/3600;

                if (StringComparer.InvariantCultureIgnoreCase.Equals("S", lattitudeRef))
                {
                    latitude = -latitude;
                }

                double longitude = longitudeComponents[0] + longitudeComponents[1]/60 +
                                   longitudeComponents[2]/3600;
                if (StringComparer.InvariantCultureIgnoreCase.Equals("W", longitudeRef))
                {
                    longitude = -longitude;
                }

                AppendMetadata(metadata, MetadataNames.Latitude, latitude);
                AppendMetadata(metadata, MetadataNames.Longitude, longitude);
            }
        }

        private static void ExtractXmpDateTime(List<PhotoMetadata> metadata, ExifReader reader)
        {
            DateTime whenTaken;
            if (reader.GetTagValue(ExifTags.DateTimeDigitized, out whenTaken))
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
            UInt32[] exposureTime;
            if (reader.GetTagValue(ExifTags.ExposureTime, out exposureTime))
            {
                AppendMetadata(metadata, MetadataNames.ExposureTime,
                               exposureTime[0]/(double) exposureTime[1]);
            }
        }

        private static void ExtractXmpFNumber(List<PhotoMetadata> metadata, ExifReader reader)
        {
            UInt32[] fNumber;
            if (reader.GetTagValue(ExifTags.FNumber, out fNumber))
            {
                AppendMetadata(metadata, MetadataNames.FNumber,
                               String.Format("F/{0}", fNumber[0]/(double)fNumber[1]));
            }
        }

        private static void ExtractXmpAperture(List<PhotoMetadata> metadata, ExifReader reader)
        {
            UInt32[] aperture;
            if (reader.GetTagValue(ExifTags.ApertureValue, out aperture))
            {
                AppendMetadata(metadata, MetadataNames.Aperture,
                               String.Format("{0}/{1}", aperture[0],aperture[1]));
            }
        }

        private static void ExtractXmpFocalLength(List<PhotoMetadata> metadata, ExifReader reader)
        {
            UInt32[] focalLength;
            if (reader.GetTagValue(ExifTags.FocalLength, out focalLength))
            {
                AppendMetadata(metadata, MetadataNames.FocalLength, focalLength[0]/(double) focalLength[1]);
            }
        }

        private static void TryIgnore(Action action)
        {
            try
            {
                action();
            }
            catch
            {
            }
        }

        private static bool IsStupidManufacturerComment(string userComment)
        {
            // Why companies put this crap in the metadata when they already set the model and manufacturer I've no idea.
            var badComments = new[] {"GE", "OLYMPUS DIGITAL CAMERA", "Minolta DSC"};

            return badComments.Any(text => StringComparer.InvariantCultureIgnoreCase.Equals(text, userComment.Trim()));
        }

        public static void AppendMetadata(List<PhotoMetadata> metadata, string name, DateTime value)
        {
            AppendMetadata(metadata, name, value.ToString(CultureInfo.InvariantCulture));
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
            Console.WriteLine(" * {0} = {1}", name, value);

            if (metadata.All(candidate => candidate.Name != name))
            {
                metadata.Add(new PhotoMetadata
                    {
                        Name = name,
                        Value = value
                    });
            }
        }
    }
}