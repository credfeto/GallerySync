using System;
using System.Collections.Generic;
using FileNaming;
using ObjectModel;

namespace UploadData
{
    [Serializable]
    public class GalleryChildItem : IEquatable<GalleryChildItem>
    {
        public List<ImageSize> ImageSizes { get; set; }

        public string Type { get; set; }

        public Location Location { get; set; }

        public DateTime DateUpdated { get; set; }

        public DateTime DateCreated { get; set; }

        public string Description { get; set; }

        public string Title { get; set; }

        public string Path { get; set; }

        public string OriginalAlbumPath { get; set; }

        public bool Equals(GalleryChildItem other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return Type == other.Type &&
                   Location == other.Location && DateUpdated == other.DateUpdated &&
                   DateCreated == other.DateCreated && Description == other.Description &&
                   Title == other.Title && Path == other.Path &&
                   OriginalAlbumPath.AsEmpty() == other.OriginalAlbumPath.AsEmpty() &&
                   ItemUpdateHelpers.CollectionEquals(ImageSizes, other.ImageSizes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GalleryChildItem) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (ImageSizes != null ? ImageSizes.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Location != null ? Location.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ DateUpdated.GetHashCode();
                hashCode = (hashCode*397) ^ DateCreated.GetHashCode();
                hashCode = (hashCode*397) ^ (Description != null ? Description.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Title != null ? Title.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Path != null ? Path.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (OriginalAlbumPath != null ? OriginalAlbumPath.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(GalleryChildItem left, GalleryChildItem right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(GalleryChildItem left, GalleryChildItem right)
        {
            return !Equals(left, right);
        }
    }
}