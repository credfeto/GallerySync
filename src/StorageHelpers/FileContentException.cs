using System;
using System.Runtime.Serialization;

namespace StorageHelpers
{
    [Serializable]
    public class FileContentException : Exception
    {
        public FileContentException(string message)
            : base(message)
        {
        }

        public FileContentException()
        {
        }

        public FileContentException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected FileContentException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}