using System;

namespace Credfeto.Gallery.OutputBuilder.Interfaces;

public interface IBrokenImageTracker
{
    void LogBrokenImage(string path, Exception exception);

    string[] AllBrokenImages();
}