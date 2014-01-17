using System;
using System.Diagnostics;

namespace Twaddle.Gallery.ObjectModel
{
    [Serializable]
    [DebuggerDisplay("Name: {Name}, Value: {Value}")]
    public class PhotoMetadata : IEquatable<PhotoMetadata>
    {
        public string Name { get; set; }

        public string Value { get; set; }

        public bool Equals(PhotoMetadata other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return Name == other.Name && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PhotoMetadata) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0)*397) ^ (Value != null ? Value.GetHashCode() : 0);
            }
        }

        public static bool operator ==(PhotoMetadata left, PhotoMetadata right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PhotoMetadata left, PhotoMetadata right)
        {
            return !Equals(left, right);
        }
    }
}