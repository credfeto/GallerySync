using System;

namespace UploadData
{
    [Serializable]
    public class Location : IEquatable<Location>
    {
        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public bool Equals(Location other)
        {
            if (ReferenceEquals(objA: null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Normalize(this.Latitude) == Normalize(other.Latitude) && Normalize(this.Longitude) == Normalize(other.Longitude);
        }

        private static long Normalize(double value)
        {
            double work = value * 1000;

            return Convert.ToInt64(work);
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

            Location other = obj as Location;

            return other != null && this.Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Normalize(this.Latitude)
                    .GetHashCode() * 397) ^ Normalize(this.Longitude)
                    .GetHashCode();
            }
        }

        public static bool operator ==(Location left, Location right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Location left, Location right)
        {
            return !Equals(left, right);
        }
    }
}