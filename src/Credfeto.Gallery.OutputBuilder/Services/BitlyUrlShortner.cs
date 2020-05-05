using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using Credfeto.Gallery.OutputBuilder.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace Credfeto.Gallery.OutputBuilder.Services
{
    /// <summary>
    ///     Bit.ly's URL Shortener.
    /// </summary>
    /// <remarks>
    ///     Get free key from https://bitly.com/a/your_api_key for up to 1000000 shortenings per day.
    /// </remarks>
    [SuppressMessage(category: "Microsoft.Naming", checkId: "CA1704:IdentifiersShouldBeSpelledCorrectly", Justification = "Bitly is name of site.")]
    public sealed class BitlyUrlShortner : IUrlShortner
    {
        private const string HTTP_CLIENT_NAME = @"Bitly";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BitlyUrlShortner> _logging;
        private readonly ISettings _settings;

        public BitlyUrlShortner(ISettings settings, IHttpClientFactory httpClientFactory, ILogger<BitlyUrlShortner> logging)
        {
            // todo: IOptions<BitlyUrlShortnerOPtions>
            this._settings = settings;
            this._httpClientFactory = httpClientFactory;
            this._logging = logging;
        }

        /// <summary>
        ///     Shortens the given URL.
        /// </summary>
        /// <param name="url">The URL to shorten.</param>
        /// <returns>
        ///     The shortened version of the URL.
        /// </returns>
        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Many possible exceptions")]
        public async Task<Uri> ShortenAsync(Uri url)
        {
            Contract.Requires(url != null);
            Contract.Ensures(Contract.Result<Uri>() != null);

            string encodedUrl = HttpUtility.UrlEncode(url.ToString());
            Uri shortnerUrl = new Uri(string.Format(provider: CultureInfo.InvariantCulture,
                                                    format: "/v3/shorten?apiKey={0}&login={1}&format=txt&longurl={2}",
                                                    arg0: this._settings.BitlyApiKey,
                                                    arg1: this._settings.BitlyApiUser,
                                                    arg2: encodedUrl),
                                      uriKind: UriKind.Relative);

            try
            {
                HttpClient client = this._httpClientFactory.CreateClient(HTTP_CLIENT_NAME);

                HttpResponseMessage response = await client.GetAsync(shortnerUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return url;
                }

                string shortened = await response.Content.ReadAsStringAsync();

                return string.IsNullOrEmpty(shortened) ? url : new Uri(shortened);
            }
            catch (Exception exception)
            {
                this._logging.LogError(new EventId(exception.HResult), exception: exception, $"Error: Could not build Short Url: {exception.Message}");

                return url;
            }
        }

        public static void RegisterServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IUrlShortner, BitlyUrlShortner>();

            const int maxRetries = 3;

            void ConfigureClient(HttpClient httpClient)
            {
                httpClient.BaseAddress = new Uri(uriString: @"https://api-ssl.bit.ly/");
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: @"application/json"));
                httpClient.DefaultRequestHeaders.Add(name: "User-Agent", value: "Credfeto.UrlShortner.Bitly");
                httpClient.DefaultRequestHeaders.Add(name: "Cache-Control", value: "no-cache");
            }

            static TimeSpan Calculate(int attempts)
            {
                return attempts > 1 ? TimeSpan.FromSeconds(Math.Pow(x: 2.0, y: attempts)) : TimeSpan.Zero;
            }

            serviceCollection.AddHttpClient(HTTP_CLIENT_NAME)
                             .ConfigureHttpClient(ConfigureClient)
                             .ConfigurePrimaryHttpMessageHandler(
                                 configureHandler: x => new HttpClientHandler {AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate})
                             .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(value: 30)))
                             .AddTransientHttpErrorPolicy(configurePolicy: p => p.WaitAndRetryAsync(retryCount: maxRetries, sleepDurationProvider: Calculate));
        }
    }
}