using System;
using System.Threading.Tasks;

namespace OutputBuilderClient.Interfaces
{
    public interface IUrlShortner
    {
        /// <summary>
        ///     Shortens the given URL.
        /// </summary>
        /// <param name="url">The URL to shorten.</param>
        /// <returns>
        ///     The shortened version of the URL.
        /// </returns>
        Task<Uri> ShortenAsync(Uri url);
    }
}