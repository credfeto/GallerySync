using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UploadData;

namespace Upload
{
    public sealed class UploadHelper
    {
        private readonly TimeSpan _timeout;
        private readonly Uri _uploadBaseAddress;

        public UploadHelper(Uri uploadBaseAddress)
            : this(uploadBaseAddress, TimeSpan.FromSeconds(600))
        {
        }

        public UploadHelper(Uri uploadBaseAddress, TimeSpan timeout)
        {
            _uploadBaseAddress = uploadBaseAddress ?? throw new ArgumentNullException(nameof(uploadBaseAddress));
            _timeout = timeout;
        }

        public async Task<bool> UploadItem(GallerySiteIndex itemToPost, string progressText,
            UploadType uploadType)
        {
            try
            {
                using (var client = new HttpClient
                {
                    BaseAddress = _uploadBaseAddress,
                    Timeout = _timeout
                })
                {
                    Console.WriteLine("Uploading ({0}): {1}", MakeUploadTypeText(uploadType), progressText);

                    const string jsonMimeType = @"application/json";

                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue(jsonMimeType));

                    var json = JsonConvert.SerializeObject(itemToPost);

                    var content = new StringContent(json, Encoding.UTF8, jsonMimeType);

                    var response = await client.PostAsync("tasks/sync", content);
                    Console.WriteLine("Status: {0}", response.StatusCode);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine(await response.Content.ReadAsStringAsync());
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error: {0}", exception.Message);
                return false;
            }
        }


        private static string MakeUploadTypeText(UploadType uploadType)
        {
            switch (uploadType)
            {
                case UploadType.NewItem:
                    return "New";

                case UploadType.DeleteItem:
                    return "Delete";

                default:
                    return "Existing";
            }
        }
    }
}