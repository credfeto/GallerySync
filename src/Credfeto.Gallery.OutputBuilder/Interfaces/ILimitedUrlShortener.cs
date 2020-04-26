using System.Threading.Tasks;

namespace OutputBuilderClient.Interfaces
{
    public interface ILimitedUrlShortener
    {
        Task<string> TryGenerateShortUrlAsync(string url);
    }
}