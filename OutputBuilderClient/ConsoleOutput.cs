using System;
using System.Threading;
using System.Threading.Tasks;

namespace OutputBuilderClient
{
    internal static class ConsoleOutput
    {
        private static readonly SemaphoreSlim _consoleSempahore = new SemaphoreSlim(1);

        internal static async Task Line(string formatString, params object[] parameters)
        {
            var text = String.Format(formatString, parameters);

            await _consoleSempahore.WaitAsync();
            try
            {
                Console.WriteLine(text);
            }
            finally
            {
                _consoleSempahore.Release();
            }
        }
    }
}