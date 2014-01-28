using System;

namespace Twaddle.Gallery.ObjectModel
{
    [Serializable]
    public sealed class FileToUpload
    {
        public string FileName { get; set; }
        public bool Completed { get; set; }
    }
}