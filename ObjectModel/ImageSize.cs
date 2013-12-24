using System;
using System.Diagnostics;

namespace Twaddle.Gallery.ObjectModel
{
    [Serializable]
    [DebuggerDisplay("Width: {Width}, Height:{Height}")]
    public class ImageSize
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
}