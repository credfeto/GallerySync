using System.Threading.Tasks;
using Images;
using ObjectModel;

namespace OutputBuilderClient
{
    public interface IShortUrls
    {
        int Count { get; }

        bool TryAdd(string longUrl, string shortUrl);

        bool TryGetValue(string url, out string shortUrl);

        Task LoadAsync();

        void LogShortUrl(string url, string shortUrl, ISettings imageSettings);

        bool ShouldGenerateShortUrl(Photo sourcePhoto, string shortUrl, string url);
    }
}