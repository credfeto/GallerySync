using System;
using System.Collections.Generic;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;

namespace Credfeto.Gallery.UploadData
{
    [Serializable]
    public class GalleryEntry : IEquatable<GalleryEntry>
    {
        public string Path { get; set; }

        public string OriginalAlbumPath { get; set; }

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

        public bool Equals(GalleryEntry other)
        {
            if (ReferenceEquals(objA: null, objB: other))
            {
                return false;
            }

            if (ReferenceEquals(this, objB: other))
            {
                return true;
            }

            return this.Path == other.Path && this.OriginalAlbumPath.AsEmpty() == other.OriginalAlbumPath.AsEmpty() && this.Title == other.Title &&
                   this.Description == other.Description && this.DateCreated == other.DateCreated && this.DateUpdated == other.DateUpdated && this.Location == other.Location &&
                   this.Rating == other.Rating && ItemUpdateHelpers.CollectionEquals(lhs: this.ImageSizes, rhs: other.ImageSizes) &&
                   ItemUpdateHelpers.CollectionEquals(lhs: this.Children, rhs: other.Children) && ItemUpdateHelpers.CollectionEquals(lhs: this.Metadata, rhs: other.Metadata) &&
                   ItemUpdateHelpers.CollectionEquals(lhs: this.Keywords, rhs: other.Keywords);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(objA: null, objB: obj))
            {
                return false;
            }

            if (ReferenceEquals(this, objB: obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((GalleryEntry) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Path != null ? this.Path.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (this.OriginalAlbumPath != null ? this.OriginalAlbumPath.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Title != null ? this.Title.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Children != null ? this.Children.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Description != null ? this.Description.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.DateCreated.GetHashCode();
                hashCode = (hashCode * 397) ^ this.DateUpdated.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.Location != null ? this.Location.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.ImageSizes != null ? this.ImageSizes.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.Rating;
                hashCode = (hashCode * 397) ^ (this.Metadata != null ? this.Metadata.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Keywords != null ? this.Keywords.GetHashCode() : 0);

                return hashCode;
            }
        }

        public static bool operator ==(GalleryEntry left, GalleryEntry right)
        {
            return Equals(objA: left, objB: right);
        }

        public static bool operator !=(GalleryEntry left, GalleryEntry right)
        {
            return !Equals(objA: left, objB: right);
        }
    }
}