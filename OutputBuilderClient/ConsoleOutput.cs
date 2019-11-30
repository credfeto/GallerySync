using System;
using System.Threading;
using System.Threading.Tasks;

namespace OutputBuilderClient
{
    internal static class ConsoleOutput
    {
        private static readonly SemaphoreSlim ConsoleSempahore = new SemaphoreSlim(initialCount: 1);

        internal static async Task Line(string formatString, params object[] parameters)
        {
            string text = string.Format(formatString, parameters);

            await ConsoleSempahore.WaitAsync();

            try
            {
                Console.WriteLine(text);
            }
            finally
            {
                ConsoleSempahore.Release();
            }
        }
    }
}