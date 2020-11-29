using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Credfeto.Gallery.ObjectModel;

namespace Credfeto.Gallery.UploadData
{
    public sealed class KeywordEntry
    {
        public KeywordEntry(string keyword)
        {
            this.Keyword = keyword;
            this.Photos = new List<Photo>();
        }

        public string Keyword { get; }

        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1002:DoNotExposeGenericLists", Justification = "Existing API")]
        public List<Photo> Photos { get; }
    }
}