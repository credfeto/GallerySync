using System;
using System.Collections.Generic;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;

namespace Credfeto.Gallery.UploadData
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
                   this.Type == other.Type && this.Previous == other.Previous && this.Next == other.Next && this.Last == other.Last &&
                   ItemUpdateHelpers.CollectionEquals(lhs: this.ImageSizes, rhs: other.ImageSizes) && ItemUpdateHelpers.CollectionEquals(lhs: this.Metadata, rhs: other.Metadata) &&
                   ItemUpdateHelpers.CollectionEquals(lhs: this.Keywords, rhs: other.Keywords) && ItemUpdateHelpers.CollectionEquals(lhs: this.Children, rhs: other.Children) &&
                   ItemUpdateHelpers.CollectionEquals(lhs: this.Breadcrumbs, rhs: other.Breadcrumbs) && this.First == other.First;
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

            return this.Equals((GalleryItem) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Path != null ? this.Path.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (this.OriginalAlbumPath != null ? this.OriginalAlbumPath.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Title != null ? this.Title.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Description != null ? this.Description.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.DateCreated.GetHashCode();
                hashCode = (hashCode * 397) ^ this.DateUpdated.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.Location != null ? this.Location.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Type != null ? this.Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.ImageSizes != null ? this.ImageSizes.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Metadata != null ? this.Metadata.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Keywords != null ? this.Keywords.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Children != null ? this.Children.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Breadcrumbs != null ? this.Breadcrumbs.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.First != null ? this.First.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Previous != null ? this.Previous.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Next != null ? this.Next.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Last != null ? this.Last.GetHashCode() : 0);

                return hashCode;
            }
        }

        public static bool operator ==(GalleryItem left, GalleryItem right)
        {
            return Equals(objA: left, objB: right);
        }

        public static bool operator !=(GalleryItem left, GalleryItem right)
        {
            return !Equals(objA: left, objB: right);
        }
    }
}