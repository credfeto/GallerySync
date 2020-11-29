using System;
using System.Collections.Generic;
using System.Linq;

namespace Credfeto.Gallery.UploadData
{
    public static class LocationHelpers
    {
        public static Location GetCenterFromDegrees(IReadOnlyList<Location> locations)
        {
            if (locations.Count == 0)
            {
                return null;
            }

            if (locations.Count == 1)
            {
                return locations.First();
            }

            return CenterFromDegreesCollection(locations);
        }

        private static Location CenterFromDegreesCollection(IEnumerable<Location> locations)
        {
            LocationNormalizer tracker = new();

            foreach (Location position in locations)
            {
                tracker.AddLocation(position);
            }

            return tracker.Location;
        }

        private sealed class LocationNormalizer
        {
            private int _count;
            private decimal _x;
            private decimal _y;
            private decimal _z;

            public Location Location
            {
                get
                {
                    if (this._count == 0)
                    {
                        return null;
                    }

                    double x2 = (double) (this._x / this._count);
                    double y2 = (double) (this._y / this._count);
                    double z2 = (double) (this._z / this._count);

                    double lon2 = Math.Atan2(y: y2, x: x2);
                    double hyp = Math.Sqrt(x2 * x2 + y2 * y2);
                    double lat2 = Math.Atan2(y: z2, x: hyp);

                    return new Location {Latitude = RadiansToDegrees(lat2), Longitude = RadiansToDegrees(lon2)};
                }
            }

            public void AddLocation(Location position)
            {
                ++this._count;
                double latRad = DegreesToRadians(position.Latitude);
                double lngRad = DegreesToRadians(position.Longitude);

                decimal a = (decimal) (Math.Cos(latRad) * Math.Cos(lngRad));
                decimal b = (decimal) (Math.Cos(latRad) * Math.Sin(lngRad));
                decimal c = (decimal) Math.Sin(latRad);

                this._x += a;
                this._y += b;
                this._z += c;
            }

            private static double DegreesToRadians(double angle)
            {
                return Math.PI / 180 * angle;
            }

            private static double RadiansToDegrees(double angle)
            {
                return angle * (180.0 / Math.PI);
            }
        }
    }
}