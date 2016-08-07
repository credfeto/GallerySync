namespace OutputBuilderClient
{
    using System;

    public class AbortProcessingException : Exception
    {
        public AbortProcessingException(string message)
            : base(message)
        {
        }
    }
}