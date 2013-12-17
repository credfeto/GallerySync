using System;
using System.Diagnostics;

namespace Twaddle.Gallery.ObjectModel
{
    [Serializable]
    [DebuggerDisplay("Width: {Width}, Height:{Height}")]
    public class ImageSize
    {
        int Width { get; set; }
        int Height { get; set; }
    }
}