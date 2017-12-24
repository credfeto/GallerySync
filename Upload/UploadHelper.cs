using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UploadData;

namespace Upload
{
    public static class UploadHelper
    {
        public static async Task<bool> UploadItem(GallerySiteIndex itemToPost, string progressText,
            UploadType uploadType)
        {
            try
            {
                using (var client = new HttpClient
                {
                    BaseAddress = new Uri(Settings.Default.WebServerBaseAddress),
                    Timeout = TimeSpan.FromSeconds(200)
                })
                {
                    Console.WriteLine("Uploading ({0}): {1}", MakeUploadTypeText(uploadType), progressText);

                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    var formatter = new JsonMediaTypeFormatter
                    {
                        SerializerSettings = {ContractResolver = new DefaultContractResolver()},
                        SupportedEncodings = {Encoding.UTF8},
                        Indent = false
                    };

                    var response = await client.PostAsync("tasks/sync", itemToPost, formatter);
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