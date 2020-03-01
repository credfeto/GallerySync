using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace OutputBuilderClient
{
    /// <summary>
    ///     Bit.ly's URL Shortener.
    /// </summary>
    /// <remarks>
    ///     Get free key from https://bitly.com/a/your_api_key for up to 1000000 shortenings per day.
    /// </remarks>
    [SuppressMessage(category: "Microsoft.Naming", checkId: "CA1704:IdentifiersShouldBeSpelledCorrectly", Justification = "Bitly is name of site.")]
    public static class BitlyUrlShortner
    {


        /// <summary>
        ///     Shortens the given URL.
        /// </summary>
        /// <param name="url">The URL to shorten.</param>
        /// <returns>
        ///     The shortened version of the URL.
        /// </returns>
        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Many possible exceptions")]
        public static async Task<Uri> Shorten(Uri url)
        {
            Contract.Requires(url != null);
            Contract.Ensures(Contract.Result<Uri>() != null);

            string encodedUrl = HttpUtility.UrlEncode(url.ToString());
            string urlRequest = string.Format(CultureInfo.InvariantCulture, format: "https://api-ssl.bit.ly/v3/shorten?apiKey={0}&login={1}&format=txt&longurl={2}", Settings.BitlyApiKey, Settings.BitlyApiUser, encodedUrl);

            Uri shortnerUrl = new Uri(urlRequest);

            try
            {
                using (HttpClient client = new HttpClient {BaseAddress = new Uri(shortnerUrl.GetLeftPart(UriPartial.Authority)), Timeout = TimeSpan.FromSeconds(value: 200)})
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));
                    client.DefaultRequestHeaders.Add(name: "Cache-Control", value: "no-cache");

                    HttpResponseMessage response = await client.GetAsync(shortnerUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        return url;
                    }

                    string shortened = await response.Content.ReadAsStringAsync();

                    return string.IsNullOrEmpty(shortened) ? url : new Uri(shortened);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(format: "Error: Could not build Short Url: {0}", exception.Message);

                // if Google's URL Shortner is down...
                return url;
            }
        }
    }
}