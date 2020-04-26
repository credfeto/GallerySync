using System.Threading.Tasks;

namespace OutputBuilderClient
{
    public interface ILimitedUrlShortener
    {
        Task<string> TryGenerateShortUrlAsync(string url);
    }
}