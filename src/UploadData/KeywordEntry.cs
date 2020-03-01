using System.Collections.Generic;
using ObjectModel;

namespace UploadData
{
    public sealed class KeywordEntry
    {
        public KeywordEntry(string keyword)
        {
            this.Keyword = keyword;
            this.Photos = new List<Photo>();
        }

        public string Keyword { get; }

        public List<Photo> Photos { get; }
    }
}