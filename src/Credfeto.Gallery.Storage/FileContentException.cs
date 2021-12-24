using System;
using System.Runtime.Serialization;

namespace Credfeto.Gallery.Storage;

[Serializable]
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

    protected FileContentException(SerializationInfo info, StreamingContext context)
        : base(info: info, context: context)
    {
    }
}