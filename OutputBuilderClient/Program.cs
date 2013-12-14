using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ExifLib;
using OutputBuilderClient.Properties;
using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Client.Embedded;
using Twaddle.Gallery.ObjectModel;

namespace OutputBuilderClient
{
    internal class Program
    {
        private static int Main()
        {
            try
            {
                ProcessGallery();

                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: {0}", exception.Message);
                return 1;
            }
        }

        private static void ProcessGallery()
        {
            string dbInputFolder = Settings.Default.DatabaseInputFolder;

            var documentStoreInput = new EmbeddableDocumentStore
                {
                    DataDirectory = dbInputFolder
                };

            documentStoreInput.Initialize();


            string dbOutputFolder = Settings.Default.DatabaseOutputFolder;
            if (!Directory.Exists(dbOutputFolder))
            {
                Directory.CreateDirectory(dbOutputFolder);
            }

            var documentStoreOutput = new EmbeddableDocumentStore
                {
                    DataDirectory = dbOutputFolder
                };

            documentStoreOutput.Initialize();

            Process(documentStoreInput, documentStoreOutput);
        }

        private static void Process(EmbeddableDocumentStore documentStoreInput,
                                    EmbeddableDocumentStore documentStoreOutput)
        {
            using (IDocumentSession inputSession = documentStoreInput.OpenSession())
            {
                foreach (Photo sourcePhoto in GetAll(inputSession))
                {
                    using (IDocumentSession outputSession = documentStoreOutput.OpenSession())
                    {
                        var targetPhoto = outputSession.Load<Photo>(sourcePhoto.PathHash);
                        bool rebuild = targetPhoto == null || HaveFilesChanged(sourcePhoto, targetPhoto);
                        if (rebuild)
                        {
                            Console.WriteLine("Rebuild: {0}", sourcePhoto.UrlSafePath);

                            sourcePhoto.Metadata = ExtractMetadata(sourcePhoto);

                            outputSession.Store(sourcePhoto, sourcePhoto.PathHash);
                            outputSession.SaveChanges();
                        }
                        else
                        {
                            Console.WriteLine("Unchanged: {0}", targetPhoto.UrlSafePath);
                        }
                    }
                }
            }
        }

        private static List<PhotoMetadata> ExtractMetadata(Photo sourcePhoto)
        {
            string rootFolder = Settings.Default.RootFolder;

            ComponentFile xmpFile =
                sourcePhoto.Files.FirstOrDefault(
                    candidate => StringComparer.InvariantCulture.Equals(".xmp", candidate.Extension));
            if (xmpFile != null)
            {
                return ExtractMetadataFromXmp(Path.Combine(rootFolder, sourcePhoto.BasePath + xmpFile.Extension));
            }

            return ExtractMetadataFromImage(Path.Combine(rootFolder, sourcePhoto.BasePath + sourcePhoto.ImageExtension));
        }

        private static List<PhotoMetadata> ExtractMetadataFromXmp(string fileName)
        {
            var metadata = new List<PhotoMetadata>();

            return metadata;
        }

        private static List<PhotoMetadata> ExtractMetadataFromImage(string fileName)
        {
            var metadata = new List<PhotoMetadata>();

            try
            {
                var reader = new ExifReader(fileName);

                DateTime whenTaken;
                if (reader.GetTagValue(ExifTags.DateTimeDigitized, out whenTaken))
                {
                    AppendMetadata(metadata, "Date Taken", whenTaken);
                }
                else if (reader.GetTagValue(ExifTags.DateTime, out whenTaken))
                {
                    AppendMetadata(metadata, "Date Taken", whenTaken);
                }
                else if (reader.GetTagValue(ExifTags.DateTimeOriginal, out whenTaken))
                {
                    AppendMetadata(metadata, "Date Taken", whenTaken);
                }

                double[] exposureTime;
                if (reader.GetTagValue(ExifTags.ExposureTime, out exposureTime))
                {
                    AppendMetadata(metadata, "Exposure Time", exposureTime[0]/exposureTime[1]);
                }

                double[] fNumber;
                if (reader.GetTagValue(ExifTags.FNumber, out fNumber))
                {
                    AppendMetadata(metadata, "Exposure Time", string.Format("F{0}", fNumber[0]/fNumber[1]));
                }

                double[] aperture;
                if (reader.GetTagValue(ExifTags.ApertureValue, out aperture))
                {
                    AppendMetadata(metadata, "Exposure Time", string.Format("{0}/{1}", aperture[0]/aperture[1]));
                }

                double[] focalLength;
                if (reader.GetTagValue(ExifTags.ApertureValue, out focalLength))
                {
                    AppendMetadata(metadata, "Focal Length", focalLength[0]/focalLength[1]);
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

                    AppendMetadata(metadata, "GPS Latitude", latitude);
                    AppendMetadata(metadata, "GPS Longitude", longitude);
                }

                int isoSpeed;
                if (reader.GetTagValue(ExifTags.ISOSpeedRatings, out isoSpeed))
                {
                    AppendMetadata(metadata, "ISO Speed", isoSpeed);
                }

                string artist;
                if (reader.GetTagValue(ExifTags.Artist, out artist))
                {
                    AppendMetadata(metadata, "Photographer", artist);
                }

                string copyright;
                if (reader.GetTagValue(ExifTags.Artist, out copyright))
                {
                    AppendMetadata(metadata, "Copyright", copyright);
                }

                string cameraMake;
                if (reader.GetTagValue(ExifTags.Make, out cameraMake))
                {
                    AppendMetadata(metadata, "Camera Manufacturer", cameraMake);
                }

                string cameraModel;
                if (reader.GetTagValue(ExifTags.Model, out cameraModel))
                {
                    AppendMetadata(metadata, "Camera Model", cameraModel);
                }
            }
            catch
            {
            }
            return metadata;
        }

        private static void AppendMetadata(List<PhotoMetadata> metadata, string name, DateTime value)
        {
            AppendMetadata(metadata, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendMetadata(List<PhotoMetadata> metadata, string name, int value)
        {
            AppendMetadata(metadata, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendMetadata(List<PhotoMetadata> metadata, string name, double value)
        {
            AppendMetadata(metadata, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendMetadata(List<PhotoMetadata> metadata, string name, string value)
        {
            Console.WriteLine(" * {0} = {1}", name, value);
            metadata.Add(new PhotoMetadata
                {
                    Name = name,
                    Value = value
                });
        }

        private static bool HaveFilesChanged(Photo sourcePhoto, Photo targetPhoto)
        {
            if (sourcePhoto.Files.Count != targetPhoto.Files.Count)
            {
                return true;
            }

            foreach (ComponentFile componentFile in targetPhoto.Files)
            {
                ComponentFile found =
                    sourcePhoto.Files.FirstOrDefault(
                        candiate =>
                        StringComparer.InvariantCultureIgnoreCase.Equals(candiate.Extension, componentFile.Extension));

                if (found != null)
                {
                    if (componentFile.Hash != found.Hash)
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<Photo> GetAll(IDocumentSession session)
        {
            using (
                IEnumerator<StreamResult<Photo>> enumerator = session.Advanced.Stream<Photo>(fromEtag: Etag.Empty,
                                                                                             start: 0,
                                                                                             pageSize: int.MaxValue))
                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current.Document;
                }
        }
    }
}