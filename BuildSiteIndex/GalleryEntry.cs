using System;
using System.Collections.Generic;
using Twaddle.Gallery.ObjectModel;

namespace BuildSiteIndex
{
    [Serializable]
    public class GalleryEntry
    {
        public string Path { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public List<GalleryEntry> Children { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateUpdated { get; set; }

        public Location Location { get; set; }

        public List<ImageSize> ImageSizes { get; set; }

        public int Rating { get; set; }

        public List<PhotoMetadata> Metadata { get; set; }

        public List<string> Keywords { get; set; }
    }

    [Serializable]
    public class Location
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}