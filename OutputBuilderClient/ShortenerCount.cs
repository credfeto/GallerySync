using System;
using System.Diagnostics;

namespace OutputBuilderClient
{
    [Serializable]
    [DebuggerDisplay("Year: {Year}, Mpnth:{Month} Impressions:{Impressions} TotalImpressionsEver:{TotalImpressionsEver}")]
    public class ShortenerCount
    {
        public int Year
        {
            get;
            set;
        }

        public int Month
        {
            get;
            set;
        }

        public int Impressions
        {
            get;
            set;
        }

        public int TotalImpressionsEver
        {
            get; set; }
    }
}