using System;
using System.Diagnostics;

namespace Twaddle.Gallery.ObjectModel
{
    [Serializable]
    [DebuggerDisplay("Extension: {Extension}, Hash:{Hash}")]
    public class ComponentFile
    {
        public string Extension { get; set; }

        public string Hash { get; set; }

        public DateTime LastModified { get; set; }

        public long FileSize { get; set; }
    }
}