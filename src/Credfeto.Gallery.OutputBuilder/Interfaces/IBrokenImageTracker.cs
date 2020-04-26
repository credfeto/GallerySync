using System;

namespace OutputBuilderClient.Interfaces
{
    public interface IBrokenImageTracker
    {
        void LogBrokenImage(string path, Exception exception);

        string[] AllBrokenImages();
    }
}