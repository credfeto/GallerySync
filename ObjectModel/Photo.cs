using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Twaddle.Gallery.ObjectModel
{
    [Serializable]
    [DebuggerDisplay("BasePath: {BasePath}, Image:{ImageExtension} Hash:{PathHash}")]
    public class Photo
    {
        private readonly Dictionary<string, ComponentFile> _files = new Dictionary<string, ComponentFile>();

        public string UrlSafePath { get; set; }

        public string BasePath { get; set; }

        public string PathHash { get; set; }

        public string ImageExtension { get; set; }

        public Dictionary<string, ComponentFile> Files
        {
            get { return _files; }
        }
    }
}