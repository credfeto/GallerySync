using System;
using System.Diagnostics;

namespace Credfeto.Gallery.ObjectModel
{
    [Serializable]
    [DebuggerDisplay(value: "Width: {Width}, Height:{Height}")]
    public class ImageSize : IEquatable<ImageSize>
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public bool Equals(ImageSize other)
        {
            if (ReferenceEquals(objA: null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Width == other.Width && this.Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(objA: null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((ImageSize) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Width * 397) ^ this.Height;
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