using System;

namespace Images
{
    public class AbortProcessingException : Exception
    {
        public AbortProcessingException(string message)
            : base(message)
        {
        }
    }
}