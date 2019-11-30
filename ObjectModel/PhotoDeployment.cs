using System;
using System.Collections.Generic;

namespace ObjectModel
{
    [Serializable]
    public class PhotoDeployment
    {
        public Dictionary<string, Photo> Photos { get; } = new Dictionary<string, Photo>();
    }
}