using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using OutputBuilderClient.Properties;

namespace OutputBuilderClient
{
    /// <summary>
    ///     Bit.ly's URL Shortener.
    /// </summary>
    /// <remarks>
    ///     Get free key from https://bitly.com/a/your_api_key for up to 1000000 shortenings per day.
    /// </remarks>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly",
        Justification = "Bitly is name of site.")]
    public static class BitlyUrlShortner
    {
        /// <summary>
        ///     The API key.
        /// </summary>
        private static string Key
        {
            get { return Settings.Default.BitlyApiKey; }
        }

        /// <summary>
        ///     The bitly username.
        /// </summary>
        private static string Login
        {
            get { return Settings.Default.BitlyApiUser; }
        }

        /// <summary>
        ///     Shortens the given URL.
        /// </summary>
        /// <param name="url">The URL to shorten.</param>
        /// <returns>
        ///     The shortened version of the URL.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Many possible exceptions")]
        public static async Task<Uri> Shorten(Uri url)
        {
            Contract.Requires(url != null);
            Contract.Ensures(Contract.Result<Uri>() != null);

            var encodedUrl = HttpUtility.UrlEncode(url.ToString());
            var urlRequest = string.Format(
                CultureInfo.InvariantCulture,
                "https://api-ssl.bit.ly/v3/shorten?apiKey={0}&login={1}&format=txt&longurl={2}",
                Key,
                Login,
                encodedUrl);

            var shortnerUrl = new Uri(urlRequest);

            try
            {
                using (var client = new HttpClient
                {
                    BaseAddress = new Uri(shortnerUrl.GetLeftPart(UriPartial.Authority)),
                    Timeout = TimeSpan.FromSeconds(200)
                })
                {
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                    var response = await client.GetAsync(shortnerUrl);
                    if (!response.IsSuccessStatusCode)
                        return url;

                    var shortened = await response.Content.ReadAsStringAsync();

                    return string.IsNullOrEmpty(shortened) ? url : new Uri(shortened);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: Could not build Short Url: {0}", exception.Message);

                // if Google's URL Shortner is down...
                return url;
            }
        }
    }
}