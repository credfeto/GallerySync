using System;
using System.Collections.Generic;
using System.Linq;

namespace UploadData
{
    public static class LocationHelpers
    {
        public static Location GetCenterFromDegrees(List<Location> locations)
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
            var tracker = new LocationNormalizer();

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
                    if (_count == 0)
                    {
                        return null;
                    }


                    var x2 = (double) (_x/_count);
                    var y2 = (double) (_y/_count);
                    var z2 = (double) (_z/_count);

                    double lon2 = Math.Atan2(y2, x2);
                    double hyp = Math.Sqrt((x2*x2) + (y2*y2));
                    double lat2 = Math.Atan2(z2, hyp);

                    return new Location
                        {
                            Latitude = RadiansToDegrees(lat2),
                            Longitude = RadiansToDegrees(lon2)
                        };
                }
            }

            public void AddLocation(Location position)
            {
                ++_count;
                double latRad = DegreesToRadians(position.Latitude);
                double lngRad = DegreesToRadians(position.Longitude);

                var a = (decimal) (Math.Cos(latRad)*Math.Cos(lngRad));
                var b = (decimal) (Math.Cos(latRad)*Math.Sin(lngRad));
                var c = (decimal) (Math.Sin(latRad));

                _x += a;
                _y += b;
                _z += c;
            }

            private static double DegreesToRadians(double angle)
            {
                return (Math.PI/180)*angle;
            }

            private static double RadiansToDegrees(double angle)
            {
                return angle*(180.0/Math.PI);
            }
        }
    }
}