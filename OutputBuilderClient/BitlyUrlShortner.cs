using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Net;
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
        #region Constants and Fields

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

        #endregion

        #region Public Methods

        /// <summary>
        ///     Shortens the given URL.
        /// </summary>
        /// <param name="url">The URL to shorten.</param>
        /// <returns>
        ///     The shortened version of the URL.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Many possible exceptions")]
        public static Uri Shorten(Uri url)
        {
            Contract.Requires(url != null);
            Contract.Ensures(Contract.Result<Uri>() != null);

            string encodedUrl = HttpUtility.UrlEncode(url.ToString());
            string urlRequest =
                string.Format(
                    CultureInfo.InvariantCulture,
                    "https://api-ssl.bit.ly/v3/shorten?apiKey={0}&login={1}&format=txt&longurl={2}",
                    Key,
                    Login,
                    encodedUrl);

            var request = (HttpWebRequest) WebRequest.Create(new Uri(urlRequest));
            try
            {
                request.ContentType = "application/json";
                request.Headers.Add("Cache-Control", "no-cache");
                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        if (responseStream == null)
                        {
                            return url;
                        }

                        using (var responseReader = new StreamReader(responseStream))
                        {
                            string shortened = responseReader.ReadToEnd();

                            return string.IsNullOrEmpty(shortened) ? url : new Uri(shortened);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // if Google's URL Shortner is down...
                return url;
            }
        }

        #endregion
    }
}