using System;

namespace OutputBuilderClient
{
    public class AbortProcessingException : Exception
    {
        public AbortProcessingException(string message)
            :base( message )
        {            
        }
    }
}