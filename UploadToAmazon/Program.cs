using NConsoler;

namespace UploadToAmazon
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Consolery.Run(typeof (Commands), args);
        }
    }
}