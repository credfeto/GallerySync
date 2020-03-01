using System;

namespace OutputBuilderClient
{
    internal static class MetadataFormatting
    {
        public static string FormatAperture(double d)
        {
            string sz = string.Empty;

            if (d != 0.0)
            {
                double dd = MetadataNormalizationFunctions.ClosestFStop(d);

                if (!double.IsInfinity(dd) && dd > 0)
                {
                    //sprintf_s(sz, len, "f/%.01f", dd);
                    sz = string.Format(format: "f/{0:0.0#}", dd);
                }
            }
            else
            {
                sz = "f/0";
            }

            return sz;
        }

        public static string FormatExposure(double d, bool bucket = true)
        {
            string sz = string.Empty; // empty

            if (d != 0.0)
            {
                //auto s = std::exp(std::log(2.0) * d);
                if (bucket)
                {
                    if (d < 1.0)
                    {
                        int n = ExpRound(1.0 / d);

                        if (n == 1)
                        {
                            sz = "1s";
                        }
                        else
                        {
                            // sprintf_s(sz, len, "1/%ds", ExpRound(1.0 / d));
                            sz = string.Format(format: "1/{0}s", n);
                        }
                    }
                    else
                    {
                        //sprintf_s(sz, len, "%ds", ExpRound(d));
                        sz = string.Format(format: "{0}s", ExpRound(d));
                    }
                }
                else
                {
                    if (d < 1.0)
                    {
                        // sprintf_s(sz, len, "1/%ds", Core::Round(1.0 / d));
                        sz = string.Format(format: "1/{0}s", Round(1.0 / d));
                    }
                    else
                    {
                        //sprintf_s(sz, len, "%ds", Core::Round(d));
                        sz = string.Format(format: "{0}s", Round(d));
                    }
                }
            }

            return sz;
        }

        public static string FormatFNumber(double d)
        {
            // sprintf_s(sz, len, "f/%.01f", d)
            return string.Format(format: "f/{0:0.#}", d);
        }

        public static string FormatFocalLength(double d, int filmEquivalent = 0)
        {
            string sz = string.Empty;

            if (d != 0.0)
            {
                if (filmEquivalent != 0)
                {
                    // sprintf_s(sz, len, "%.1fmm (%dmm film eq)", d, filmEquivalent);
                    sz = string.Format(format: "{0:0.#}mm ({1}mm film eq)", d, filmEquivalent);
                }
                else
                {
                    //sprintf_s(sz, len, d < 1 ? "%.1fmm" : "%.0fmm", d);
                    sz = string.Format(d < 1 ? "{0:0.#}mm" : "{0:0}mm", d);
                }
            }
            else
            {
                sz = "0mm";
            }

            return sz;
        }

        private static int ExpRound(double d)
        {
            int n = Round(d);

            if (n >= 950)
            {
                return Round(n, y: 1000);
            }

            if (n >= 95)
            {
                return Round(n, y: 100);
            }

            if (n >= 5)
            {
                return Round(n, y: 10);
            }

            return n;
        }

        private static int Round(double d)
        {
            if (double.IsNaN(d))
            {
                return 0;
            }

            double f = Math.Floor(d);

            if (d - f >= 0.5)
            {
                return (int) (d >= 0.0 ? Math.Ceiling(d) : f);
            }

            return (int) (d < 0.0 ? Math.Ceiling(d) : f);
        }

        private static int Round(int x, int y)
        {
            return (x + y / 2) / y * y;
        }

        private static long Round(long x, long y)
        {
            return (x + y / 2) / y * y;
        }
    }
}