using System.Collections.Generic;

namespace Twaddle.Directory.Scanner
{
    public class FileEntry
    {
        public string Folder { get; set; }
        public string RelativeFolder { get; set; }
        public string LocalFileName { get; set; }

        public List<string> AlternateFileNames { get; set; }
    }
}