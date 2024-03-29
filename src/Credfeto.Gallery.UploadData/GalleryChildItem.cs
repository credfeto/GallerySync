﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;

namespace Credfeto.Gallery.UploadData;

[Serializable]
public class GalleryChildItem : IEquatable<GalleryChildItem>
{
    [SuppressMessage(category: "Microsoft.Design", checkId: "CA1002:DoNotExposeGenericLists", Justification = "Existing API")]
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
        if (ReferenceEquals(objA: null, objB: other))
        {
            return false;
        }

        if (ReferenceEquals(this, objB: other))
        {
            return true;
        }

        return this.Type == other.Type && this.Location == other.Location && this.DateUpdated == other.DateUpdated && this.DateCreated == other.DateCreated && this.Description == other.Description &&
               this.Title == other.Title && this.Path == other.Path && this.OriginalAlbumPath.AsEmpty() == other.OriginalAlbumPath.AsEmpty() &&
               ItemUpdateHelpers.CollectionEquals(lhs: this.ImageSizes, rhs: other.ImageSizes);
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

        return this.Equals((GalleryChildItem)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = this.ImageSizes != null ? this.ImageSizes.GetHashCode() : 0;
            hashCode = (hashCode * 397) ^ (this.Type != null ? this.Type.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (this.Location != null ? this.Location.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ this.DateUpdated.GetHashCode();
            hashCode = (hashCode * 397) ^ this.DateCreated.GetHashCode();
            hashCode = (hashCode * 397) ^ (this.Description != null ? this.Description.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (this.Title != null ? this.Title.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (this.Path != null ? this.Path.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (this.OriginalAlbumPath != null ? this.OriginalAlbumPath.GetHashCode() : 0);

            return hashCode;
        }
    }

    public static bool operator ==(GalleryChildItem left, GalleryChildItem right)
    {
        return Equals(objA: left, objB: right);
    }

    public static bool operator !=(GalleryChildItem left, GalleryChildItem right)
    {
        return !Equals(objA: left, objB: right);
    }
}