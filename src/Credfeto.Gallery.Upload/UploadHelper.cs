using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Gallery.UploadData;

namespace Credfeto.Gallery.Upload
{
    public sealed class UploadHelper
    {
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly TimeSpan _timeout;
        private readonly Uri _uploadBaseAddress;

        public UploadHelper(Uri uploadBaseAddress)
            : this(uploadBaseAddress: uploadBaseAddress, TimeSpan.FromSeconds(value: 600))
        {
        }

        public UploadHelper(Uri uploadBaseAddress, TimeSpan timeout)
        {
            this._uploadBaseAddress = uploadBaseAddress ?? throw new ArgumentNullException(nameof(uploadBaseAddress));
            this._timeout = timeout;

            this._serializerOptions = new JsonSerializerOptions
                                      {
                                          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                          DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                                          IgnoreNullValues = true,
                                          WriteIndented = true,
                                          PropertyNameCaseInsensitive = true
                                      };
        }

        public async Task<bool> UploadItemAsync(GallerySiteIndex itemToPost, string progressText, UploadType uploadType)
        {
            try
            {
                using (HttpClient client = new HttpClient {BaseAddress = this._uploadBaseAddress, Timeout = this._timeout})
                {
                    Console.WriteLine(format: "Uploading ({0}): {1}", MakeUploadTypeText(uploadType), arg1: progressText);

                    const string jsonMimeType = @"application/json";

                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(jsonMimeType));

                    string json = JsonSerializer.Serialize(value: itemToPost, options: this._serializerOptions);

                    using (StringContent content = new StringContent(content: json, encoding: Encoding.UTF8, mediaType: jsonMimeType))
                    {
                        HttpResponseMessage response = await client.PostAsync(new Uri(uriString: "tasks/sync", uriKind: UriKind.Relative), content: content);
                        Console.WriteLine(format: "Status: {0}", arg0: response.StatusCode);

                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine(await response.Content.ReadAsStringAsync());

                            return true;
                        }

                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(format: "Error: {0}", arg0: exception.Message);

                return false;
            }
        }

        private static string MakeUploadTypeText(UploadType uploadType)
        {
            switch (uploadType)
            {
                case UploadType.NEW_ITEM: return "New";

                case UploadType.DELETE_ITEM: return "Delete";

                default: return "Existing";
            }
        }
    }
}