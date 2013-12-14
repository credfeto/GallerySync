using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ExifLib;
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

            if (ExtractXmpMetadata(sourcePhoto, metadata, rootFolder))
            {
                return metadata;
            }

            foreach (
                ComponentFile extension in
                    sourcePhoto.Files.Where(
                        candidate => !IsXmp(candidate)))
            {
                string filename = Path.Combine(rootFolder, sourcePhoto.BasePath + extension.Extension);

                ExtractMetadataFromXmp(metadata, filename);
                ExtractMetadataFromImage(metadata, filename);
            }


            return metadata;
        }

        private static bool ExtractXmpMetadata(Photo sourcePhoto, List<PhotoMetadata> metadata, string rootFolder)
        {
            ComponentFile xmpFile =
                sourcePhoto.Files.FirstOrDefault(
                    IsXmp);

            if (xmpFile != null)            
            {
                var sidecarFileName = Path.Combine(rootFolder,sourcePhoto.BasePath + xmpFile.Extension);
                ExtractMetadataFromXmpSideCar(metadata, sidecarFileName );
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
                    ExtractMetadataFromXmpSideCar(metadata, xmpFileName);
                    if (metadata.Any())
                    {
                        return true;
                    }
                }
            }

            return false;
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
                return ;
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
            if (!String.IsNullOrWhiteSpace(tag.Comment))
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
            try
            {
                var reader = new ExifReader(fileName);

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

                double[] exposureTime;
                if (reader.GetTagValue(ExifTags.ExposureTime, out exposureTime))
                {
                    AppendMetadata(metadata, MetadataNames.ExposureTime, exposureTime[0]/exposureTime[1]);
                }

                double[] fNumber;
                if (reader.GetTagValue(ExifTags.FNumber, out fNumber))
                {
                    AppendMetadata(metadata, MetadataNames.FNumber, String.Format("F/{0}", fNumber[0]/fNumber[1]));
                }

                double[] aperture;
                if (reader.GetTagValue(ExifTags.ApertureValue, out aperture))
                {
                    AppendMetadata(metadata, MetadataNames.Aperture, String.Format("{0}/{1}", aperture[0]/aperture[1]));
                }

                double[] focalLength;
                if (reader.GetTagValue(ExifTags.ApertureValue, out focalLength))
                {
                    AppendMetadata(metadata, MetadataNames.FocalLength, focalLength[0]/focalLength[1]);
                }

                double[] latitudeComponents;
                double[] longitudeComponents;
                string lattitudeRef;
                string longitudeRef;
                if (reader.GetTagValue(ExifTags.GPSLatitude, out latitudeComponents) &&
                    reader.GetTagValue(ExifTags.GPSLongitude, out longitudeComponents) &&
                    reader.GetTagValue(ExifTags.GPSLatitudeRef, out lattitudeRef) &&
                    reader.GetTagValue(ExifTags.GPSLongitudeRef, out longitudeRef))
                {
                    double latitude = latitudeComponents[0] + latitudeComponents[1]/60 + latitudeComponents[2]/3600;
                    double longitude = longitudeComponents[0] + longitudeComponents[1]/60 + longitudeComponents[2]/3600;

                    if (StringComparer.InvariantCultureIgnoreCase.Equals("S", lattitudeRef))
                    {
                        latitude = -latitude;
                    }

                    if (StringComparer.InvariantCultureIgnoreCase.Equals("W", longitudeRef))
                    {
                        longitude = -longitude;
                    }

                    AppendMetadata(metadata, MetadataNames.Latitude, latitude);
                    AppendMetadata(metadata, MetadataNames.Longitude, longitude);
                }

                int isoSpeed;
                if (reader.GetTagValue(ExifTags.ISOSpeedRatings, out isoSpeed))
                {
                    AppendMetadata(metadata, MetadataNames.ISOSpeed, isoSpeed);
                }

                string artist;
                if (reader.GetTagValue(ExifTags.Artist, out artist))
                {
                    AppendMetadata(metadata, MetadataNames.Photographer, artist);
                }

                string copyright;
                if (reader.GetTagValue(ExifTags.Artist, out copyright))
                {
                    AppendMetadata(metadata, MetadataNames.Copyright, copyright);
                }

                string cameraMake;
                if (reader.GetTagValue(ExifTags.Make, out cameraMake))
                {
                    AppendMetadata(metadata, MetadataNames.CameraManufacturer, cameraMake);
                }

                string cameraModel;
                if (reader.GetTagValue(ExifTags.Model, out cameraModel))
                {
                    AppendMetadata(metadata, MetadataNames.CameraModel, cameraModel);
                }
            }
            catch
            {
            }
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