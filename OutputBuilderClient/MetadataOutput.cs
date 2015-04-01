using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GraphicsMagick;

namespace OutputBuilderClient
{
    internal static class MetadataOutput
    {
        public static void SetLicensing(IptcProfile iptcProfile, string licensing)
        {
            iptcProfile.SetValue(IptcTag.SpecialInstructions, Encoding.UTF8, licensing);
        }

        public static void SetTransmissionReference(string url, IptcProfile iptcProfile)
        {
            iptcProfile.SetValue(IptcTag.OriginalTransmissionReference, Encoding.UTF8, url);
        }

        public static void SetProgram(IptcProfile iptcProfile, string program)
        {
            iptcProfile.SetValue(IptcTag.OriginatingProgram, Encoding.UTF8, program);
        }

        public static void SetCredit(IptcProfile iptcProfile, string credit)
        {
            iptcProfile.SetValue(IptcTag.Credit, Encoding.UTF8, credit);
        }

        public static void SetCopyright(IptcProfile iptcProfile, string copyright)
        {
            iptcProfile.SetValue(IptcTag.CopyrightNotice, Encoding.UTF8, copyright);
        }

        public static void SetDescription(string description, IptcProfile iptcProfile)
        {
            if (!String.IsNullOrWhiteSpace(description))
            {
                iptcProfile.SetValue(IptcTag.Caption, Encoding.UTF8, description);
            }
        }

        public static void SetTitle(string title, IptcProfile iptcProfile)
        {
            if (!String.IsNullOrWhiteSpace(title))
            {
                iptcProfile.SetValue(IptcTag.Headline, Encoding.UTF8, title);

                iptcProfile.SetValue(IptcTag.Title, Encoding.UTF8, title);
            }
        }

        public static void SetProgram(ExifProfile exifProfile, string program)
        {
            exifProfile.SetValue(ExifTag.ImageDescription, program);
        }

        public static void SetPhotographer(ExifProfile exifProfile, string credit)
        {
            exifProfile.SetValue(ExifTag.Artist,
                                 credit);
        }

        public static void SetLicensing(ExifProfile exifProfile, string licensing)
        {
            exifProfile.SetValue(ExifTag.UserComment,
                                 Encoding.UTF8.GetBytes(
                                     licensing));
        }

        public static void SetCopyright(ExifProfile exifProfile, string copyright)
        {
            exifProfile.SetValue(ExifTag.Copyright, copyright);
        }

        public static void SetDescription(string description, ExifProfile exifProfile)
        {
            if (!String.IsNullOrWhiteSpace(description))
            {
                exifProfile.SetValue(ExifTag.ImageDescription, description);
            }
        }

        public static void SetCreationDate(DateTime creationDate, ExifProfile exifProfile)
        {
            if (creationDate != DateTime.MinValue)
            {
                exifProfile.SetValue(ExifTag.DateTime, creationDate.Date.ToString("yyyy-MM-dd"));
            }
        }

        public static void SetCreationDate(DateTime creationDate, IptcProfile iptcProfile)
        {
            if (creationDate != DateTime.MinValue)
            {
                iptcProfile.SetValue(IptcTag.CreatedDate, creationDate.Date.ToString("yyyy-MM-dd"));
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
    }
}
