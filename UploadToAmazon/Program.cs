using System;
using System.Diagnostics;
using NConsoler;

namespace UploadToAmazon
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            BoostPriority();

            Consolery.Run(typeof (Commands), args);
        }

        private static void BoostPriority()
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass =
                    ProcessPriorityClass.High;
            }
            catch (Exception)
            {
            }
        }
    }
}