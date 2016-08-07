namespace OutputBuilderClient
{
    using System;
    using System.Diagnostics;

    [Serializable]
    [DebuggerDisplay("Year: {Year}, Mpnth:{Month} Impressions:{Impressions} TotalImpressionsEver:{TotalImpressionsEver}"
        )]
    public class ShortenerCount
    {
        public int Impressions { get; set; }

        public int Month { get; set; }

        public int TotalImpressionsEver { get; set; }

        public int Year { get; set; }
    }
}