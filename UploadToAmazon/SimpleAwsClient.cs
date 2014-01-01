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
                using (var x = new TransferUtility(client))
                {
                    x.UploadDirectory(localSourceFolder, bucketName, "*.js", SearchOption.TopDirectoryOnly);
                }

                using (var x = new TransferUtility(client))
                {
                    x.UploadDirectory(localSourceFolder, bucketName, "*.jpg", SearchOption.AllDirectories);
                }
            }
        }
    }
}