using System.Collections.Generic;
using Twaddle.Gallery.ObjectModel;

namespace BuildSiteIndex
{
    internal sealed class KeywordEntry
    {
        private readonly string _keyword;
        private readonly List<Photo> _photos;

        public KeywordEntry(string keyword)
        {
            _keyword = keyword;
            _photos = new List<Photo>();
        }

        public string Keyword
        {
            get { return _keyword; }
        }

        public List<Photo> Photos
        {
            get { return _photos; }
        }
    }
}
