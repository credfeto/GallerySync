using System;
using Amazon;
using Amazon.S3;
using Amazon.S3.IO;
using UploadToAmazon.Properties;

namespace UploadToAmazon
{
    internal static class SimpleAwsClient
    {
        public static void Upload(string awsAccessKey, string awsSecretAccessKey, RegionEndpoint regionEndpoint,
                                  string bucketName, string localSourceFolder)
        {
            using (
                IAmazonS3 client = AWSClientFactory.CreateAmazonS3Client(awsAccessKey, awsSecretAccessKey,
                                                                         regionEndpoint))
            {
                var rootDirectory = new S3DirectoryInfo(client, bucketName);
                rootDirectory.Create();


                DateTime updateSTartTime = DateTime.UtcNow;
                DateTime lastSuccessfulUpdloadDate = Settings.Default.LastSuccessfulUploadDate;
                if (lastSuccessfulUpdloadDate == DateTime.MinValue)
                {
                    rootDirectory.CopyFromLocal(localSourceFolder);
                }
                else
                {
                    rootDirectory.CopyFromLocal(localSourceFolder, lastSuccessfulUpdloadDate);
                }

                Settings.Default.LastSuccessfulUploadDate = updateSTartTime;
            }
        }
    }
}