using System;
using System.Collections.Generic;
using FileNaming;
using Twaddle.Gallery.ObjectModel;

namespace BuildSiteIndex
{
    [Serializable]
    public class GalleryItem : IEquatable<GalleryItem>
    {
        public string Path { get; set; }

        public string OriginalAlbumPath { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateUpdated { get; set; }

        public Location Location { get; set; }

        public string Type { get; set; }

        public List<ImageSize> ImageSizes { get; set; }

        public List<PhotoMetadata> Metadata { get; set; }

        public List<string> Keywords { get; set; }

        public List<GalleryChildItem> Breadcrumbs { get; set; }

        public List<GalleryChildItem> Children { get; set; }

        public GalleryChildItem First { get; set; }

        public GalleryChildItem Previous { get; set; }

        public GalleryChildItem Next { get; set; }

        public GalleryChildItem Last { get; set; }


        public bool Equals(GalleryItem other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return Path == other.Path &&
                   OriginalAlbumPath.AsEmpty() == other.OriginalAlbumPath.AsEmpty()
                   && Title == other.Title &&
                   Description == other.Description && DateCreated == other.DateCreated &&
                   DateUpdated == other.DateUpdated && Location == other.Location &&
                   Type == other.Type &&
                   Previous == other.Previous &&
                   Next == other.Next && Last == other.Last &&
                   ItemUpdateHelpers.CollectionEquals(ImageSizes, other.ImageSizes) &&
                   ItemUpdateHelpers.CollectionEquals(Metadata, other.Metadata) &&
                   ItemUpdateHelpers.CollectionEquals(Keywords, other.Keywords) &&
                   ItemUpdateHelpers.CollectionEquals(Children, other.Children) &&
                   ItemUpdateHelpers.CollectionEquals(Breadcrumbs, other.Breadcrumbs) &&
                   First == other.First;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GalleryItem) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Path != null ? Path.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (OriginalAlbumPath != null ? OriginalAlbumPath.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Title != null ? Title.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Description != null ? Description.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ DateCreated.GetHashCode();
                hashCode = (hashCode*397) ^ DateUpdated.GetHashCode();
                hashCode = (hashCode*397) ^ (Location != null ? Location.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (ImageSizes != null ? ImageSizes.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Metadata != null ? Metadata.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Keywords != null ? Keywords.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Children != null ? Children.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Breadcrumbs != null ? Breadcrumbs.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (First != null ? First.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Previous != null ? Previous.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Next != null ? Next.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Last != null ? Last.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(GalleryItem left, GalleryItem right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(GalleryItem left, GalleryItem right)
        {
            return !Equals(left, right);
        }
    }
}