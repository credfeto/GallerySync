using System;
using System.Collections.Generic;

namespace Twaddle.Gallery.ObjectModel
{
    [Serializable]
    public class PhotoDeployment
    {
        private readonly Dictionary<string, Photo> _photos = new Dictionary<string, Photo>();

        public Dictionary<string, Photo> Photos
        {
            get { return _photos; }
        }
    }
}