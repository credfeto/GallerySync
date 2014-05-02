using System;
using System.Collections.Generic;

namespace BuildSiteIndex
{
    internal static class LocationHelpers
    {
        public static Location GetCenterFromDegrees(IEnumerable<Location> locations)
        {
            double x = 0.0;
            double y = 0.0;
            double z = 0.0;

            int count = 0;
            foreach (Location position in locations)
            {
                ++count;
                double latRad = DegreesToRadians(position.Latitude);
                double lngRad = DegreesToRadians(position.Longitude);

                double a = Math.Cos(latRad)*Math.Cos(lngRad);
                double b = Math.Cos(latRad)*Math.Sin(lngRad);
                double c = Math.Sin(latRad);

                x += a;
                y += b;
                z += c;
            }

            if (count == 0)
            {
                return null;
            }


            double x2 = x/count;
            double y2 = y/count;
            double z2 = z/count;

            double lon2 = Math.Atan2(y2, x2);
            double hyp = Math.Sqrt((x2*x2) + (y2*y2));
            double lat2 = Math.Atan2(z2, hyp);

            return new Location
                {
                    Latitude = RadiansToDegrees(lat2),
                    Longitude = RadiansToDegrees(lon2)
                };
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