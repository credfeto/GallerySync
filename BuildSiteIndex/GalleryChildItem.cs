using System;
using System.Collections.Generic;
using Twaddle.Gallery.ObjectModel;

namespace BuildSiteIndex
{
    [Serializable]
    public class GalleryChildItem
    {
        public List<ImageSize> ImageSizes { get; set; }


        public string Type { get; set; }


        public Location Location { get; set; }

        public DateTime DateUpdated { get; set; }

        public DateTime DateCreated { get; set; }

        public string Description { get; set; }

        public string Title { get; set; }

        public string Path { get; set; }
    }
}