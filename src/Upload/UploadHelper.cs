using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UploadData;

namespace Upload
{
    public sealed class UploadHelper
    {
        private readonly TimeSpan _timeout;
        private readonly Uri _uploadBaseAddress;

        public UploadHelper(Uri uploadBaseAddress)
            : this(uploadBaseAddress, TimeSpan.FromSeconds(value: 600))
        {
        }

        public UploadHelper(Uri uploadBaseAddress, TimeSpan timeout)
        {
            this._uploadBaseAddress = uploadBaseAddress ?? throw new ArgumentNullException(nameof(uploadBaseAddress));
            this._timeout = timeout;
        }

        public async Task<bool> UploadItemAsync(GallerySiteIndex itemToPost, string progressText, UploadType uploadType)
        {
            try
            {
                using (HttpClient client = new HttpClient {BaseAddress = this._uploadBaseAddress, Timeout = this._timeout})
                {
                    Console.WriteLine(format: "Uploading ({0}): {1}", MakeUploadTypeText(uploadType), progressText);

                    const string jsonMimeType = @"application/json";

                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(jsonMimeType));

                    string json = JsonSerializer.Serialize(itemToPost);

                    using (StringContent content = new StringContent(json, Encoding.UTF8, jsonMimeType))
                    {
                        HttpResponseMessage response = await client.PostAsync(new Uri(uriString: "tasks/sync", UriKind.Relative), content);
                        Console.WriteLine(format: "Status: {0}", response.StatusCode);

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
                Console.WriteLine(format: "Error: {0}", exception.Message);

                return false;
            }
        }

        private static string MakeUploadTypeText(UploadType uploadType)
        {
            switch (uploadType)
            {
                case UploadType.NewItem: return "New";

                case UploadType.DeleteItem: return "Delete";

                default: return "Existing";
            }
        }
    }
}