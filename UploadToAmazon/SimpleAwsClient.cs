using System;
using System.IO;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;

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
                using (var transferUtility = new TransferUtility(client))
                {
                    UploadDirectory(transferUtility, localSourceFolder, bucketName, "*.js",
                                    SearchOption.TopDirectoryOnly);

                    UploadDirectory(transferUtility, localSourceFolder, bucketName, "*.jpg", SearchOption.AllDirectories);
                }
            }
        }

        private static void UploadDirectory(TransferUtility transferUtility, string localSourceFolder, string bucketName, 
                                            string searchPattern, SearchOption searchOption)
        {
            // TODO: Work out how to ignore what's aready there
            var req = new TransferUtilityUploadDirectoryRequest
                {
                    BucketName = bucketName,
                    Directory = localSourceFolder,
                    SearchPattern = searchPattern,
                    SearchOption = searchOption
                };

            req.UploadDirectoryProgressEvent += req_UploadDirectoryProgressEvent;
            transferUtility.UploadDirectory(req);
        }

        private static void req_UploadDirectoryProgressEvent(object sender, UploadDirectoryProgressArgs e)
        {
            double transferProgress = (e.TransferredBytesForCurrentFile / (double)e.TotalNumberOfBytesForCurrentFile) * 100;
            Console.WriteLine("\rUploaded: {0} ({1}%)", e.CurrentFile, transferProgress );
        }
    }
}