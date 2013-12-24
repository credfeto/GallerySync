using Amazon;
using NConsoler;

namespace UploadToAmazon
{
    internal static class Commands
    {
        [Action("Upload a folder to AWS")]
        public static void Upload([Required(Description = "AWS Access Key")] string awsAccessKey, [Required(Description = "AWS Access Key")] string awsSecretAccessKey,
                                  [Required(Description = "AWS Region e.g. us-east-1")] string regionEndpoint,
                                  [Required(Description = "Bucket to upload from")] string bucketName, [Required(Description = "Where to upload files from")] string localSourceFolder)
        {
            RegionEndpoint region = RegionEndpoint.GetBySystemName(regionEndpoint) ?? RegionEndpoint.USEast1;

            SimpleAwsClient.Upload(awsAccessKey, awsSecretAccessKey, region, bucketName, localSourceFolder);
        }
    }
}