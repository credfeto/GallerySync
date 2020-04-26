using System;

namespace Credfeto.Gallery.OutputBuilder
{
    internal static class MetadataNormalizationFunctions
    {
        public static double ClosestFStop(double d)
        {
            double[] stops =
            {
                0.7,
                0.8,
                0.9,
                1.0,
                1.1,
                1.2,
                1.3,
                1.4,
                1.5,
                1.6,
                1.7,
                1.8,
                2,
                2.2,
                2.4,
                2.5,
                2.6,
                2.8,
                3.2,
                3.3,
                3.4,
                3.5,
                3.7,
                32,
                4,
                4.4,
                4.5,
                4.8,
                5.0,
                5.2,
                5.6,
                6.2,
                6.3,
                6.7,
                7.1,
                7.3,
                8,
                8.7,
                9,
                9.5,
                10,
                11,
                12,
                13,
                14,
                15,
                16,
                17,
                18,
                19,
                20,
                21,
                22,
                27
            };

            double fs = Math.Exp(Math.Log(d: 2.0) * d / 2);

            int upper = 0;

            while (upper < stops.Length && stops[upper] <= fs)
            {
                ++upper;
            }

            if (upper == stops.Length)
            {
                return stops[stops.Length - 1];
            }

            if (upper == 0)
            {
                return stops[0];
            }

            int lower = upper - 1;

            return Math.Abs(stops[lower] - fs) < Math.Abs(stops[upper] - fs) ? stops[lower] : stops[upper];
        }

        public static double ToApexValue(double d)
        {
            // Invers APR 
            double result = Math.Log(d) * 2.0 / Math.Log(d: 2.0);

            return result;
        }

        public static double ToReal(uint numerator, uint denominator)
        {
            if (denominator == 0)
            {
                return 0.0;
            }

            return numerator / (double) denominator;
        }
    }
}