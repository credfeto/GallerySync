using System;
using System.Collections.Generic;
using Twaddle.Gallery.ObjectModel;

namespace BuildSiteIndex
{
    [Serializable]
    public class GalleryItem
    {
        public string Path { get; set; }

        public string Title { get; set; }


        public string Description { get; set; }


        public DateTime DateCreated { get; set; }


        public DateTime DateUpdated { get; set; }


        public Location Location { get; set; }


        public string Type { get; set; }

        public List<ImageSize> ImageSizes { get; set; }


        public List<PhotoMetadata> Metadata { get; set; }


        public List<string> Keywords { get; set; }


        public List<GalleryChildItem> Children { get; set; }
    }
}