using System;

namespace Credfeto.Gallery.Storage;

public sealed class FileContentException : Exception
{
    public FileContentException(string message)
        : base(message)
    {
    }

    public FileContentException()
    {
    }

    public FileContentException(string message, Exception innerException)
        : base(message: message, innerException: innerException)
    {
    }
}