using System;
using System.Threading;
using System.Threading.Tasks;

namespace OutputBuilderClient
{
    internal static class ConsoleOutput
    {
        private static readonly SemaphoreSlim _consoleSempahore = new SemaphoreSlim(initialCount: 1);

        internal static async Task Line(string formatString, params object[] parameters)
        {
            string text = string.Format(formatString, parameters);

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