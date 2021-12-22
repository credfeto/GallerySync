﻿using System;
using System.Diagnostics;

namespace Credfeto.Gallery.ObjectModel;

[Serializable]
[DebuggerDisplay(value: "Name: {Name}, Value: {Value}")]
public class PhotoMetadata : IEquatable<PhotoMetadata>
{
    public string Name { get; set; }

    public string Value { get; set; }

    public bool Equals(PhotoMetadata other)
    {
        if (ReferenceEquals(objA: null, objB: other))
        {
            return false;
        }

        if (ReferenceEquals(this, objB: other))
        {
            return true;
        }

        return this.Name == other.Name && this.Value == other.Value;
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

        return this.Equals((PhotoMetadata)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((this.Name != null ? this.Name.GetHashCode() : 0) * 397) ^ (this.Value != null ? this.Value.GetHashCode() : 0);
        }
    }

    public static bool operator ==(PhotoMetadata left, PhotoMetadata right)
    {
        return Equals(objA: left, objB: right);
    }

    public static bool operator !=(PhotoMetadata left, PhotoMetadata right)
    {
        return !Equals(objA: left, objB: right);
    }
}