using System.Threading.Tasks;
using Credfeto.Gallery.ObjectModel;

namespace Credfeto.Gallery.OutputBuilder.Interfaces
{
    public interface IShortUrls
    {
        int Count { get; }

        bool TryAdd(string longUrl, string shortUrl);

        bool TryGetValue(string url, out string shortUrl);

        Task LoadAsync();

        Task LogShortUrlAsync(string url, string shortUrl);

        bool ShouldGenerateShortUrl(Photo sourcePhoto, string shortUrl, string url);
    }
}