using System;
using System.Diagnostics;

namespace Credfeto.Gallery.OutputBuilder;

[Serializable]
[DebuggerDisplay(value: "Year: {Year}, Month:{Month} Impressions:{Impressions} TotalImpressionsEver:{TotalImpressionsEver}")]
public class ShortenerCount
{
    public int Impressions { get; set; }

    public int Month { get; set; }

    public long TotalImpressionsEver { get; set; }

    public int Year { get; set; }
}