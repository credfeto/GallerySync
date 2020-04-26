using System;
using System.IO;
using System.Text;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace Credfeto.Gallery.Image
{
    internal static class MetadataOutput
    {
        public static void SetCopyright(ExifProfile exifProfile, string copyright)
        {
            exifProfile.SetValue(ExifTag.Copyright, copyright);
        }

        public static void SetCreationDate(DateTime creationDate, ExifProfile exifProfile)
        {
            if (creationDate != DateTime.MinValue)
            {
                exifProfile.SetValue(ExifTag.DateTime, creationDate.Date.ToString(format: "yyyy-MM-dd"));
            }
        }

        public static void SetCreationDate(string fileName, DateTime creationDate)
        {
            if (creationDate != DateTime.MinValue && File.Exists(fileName))
            {
                File.SetCreationTimeUtc(fileName, creationDate);
                File.SetLastWriteTimeUtc(fileName, creationDate);
                File.SetLastAccessTimeUtc(fileName, creationDate);
            }
        }

        public static void SetDescription(string description, ExifProfile exifProfile)
        {
            if (!string.IsNullOrWhiteSpace(description))
            {
                exifProfile.SetValue(ExifTag.ImageDescription, description);
            }
        }

        public static void SetLicensing(ExifProfile exifProfile, string licensing)
        {
            exifProfile.SetValue(ExifTag.UserComment, Encoding.UTF8.GetBytes(licensing));
        }

        public static void SetPhotographer(ExifProfile exifProfile, string credit)
        {
            exifProfile.SetValue(ExifTag.Artist, credit);
        }

        public static void SetProgram(ExifProfile exifProfile, string program)
        {
            exifProfile.SetValue(ExifTag.ImageDescription, program);
        }
    }
}