using System;
using System.Diagnostics;

namespace Twaddle.Gallery.ObjectModel
{
    [Serializable]
    [DebuggerDisplay("Name: {Name}, Value: {Value}")]
    public class PhotoMetadata
    {
        public string Name { get; set; }

        public string Value { get; set; }
    }
}