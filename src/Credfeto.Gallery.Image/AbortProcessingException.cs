using System;

namespace Credfeto.Gallery.Image
{
    public class AbortProcessingException : Exception
    {
        public AbortProcessingException()
            : this(message: "Processing aborted")
        {
        }

        public AbortProcessingException(string message)
            : base(message)
        {
        }

        public AbortProcessingException(string message, Exception innerException)
            : base(message: message, innerException: innerException)
        {
        }
    }
}