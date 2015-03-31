namespace OutputBuilderClient
{
    public static class Constants
    {
        public const string DefaultShortUrl = "https://www.markridgwell.co.uk/";

        // V1 = Original
        // V2 = fix to GPS...  broke Image sizes
        // V3 = fix image sizes
        // V4 = Another metadata fix - so it doesn't randomly wipe out items
        // V5 = Broken images - force re-generate of all images
        public const int CurrentMetadataVersion = 5;
    }

    public static class MetadataVersionHelpers
    {
        public static bool RequiresRebuild(int version)
        {
            if (version < 5)
            {
                // Broken images - force re-generate of all images
                return true;
            }

            return false;
        }

        public static bool IsOutOfDate(int version)
        {
            return version < Constants.CurrentMetadataVersion;
        }
    }
}