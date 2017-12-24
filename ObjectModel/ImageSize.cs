using System;
using System.Diagnostics;

namespace ObjectModel
{
    [Serializable]
    [DebuggerDisplay("Width: {Width}, Height:{Height}")]
    public class ImageSize : IEquatable<ImageSize>
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public bool Equals(ImageSize other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ImageSize) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width*397) ^ Height;
            }
        }

        public static bool operator ==(ImageSize left, ImageSize right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ImageSize left, ImageSize right)
        {
            return !Equals(left, right);
        }
    }
}