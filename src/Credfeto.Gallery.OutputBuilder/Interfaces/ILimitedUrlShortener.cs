using System.Threading.Tasks;

namespace Credfeto.Gallery.OutputBuilder.Interfaces;

public interface ILimitedUrlShortener
{
    Task<string> TryGenerateShortUrlAsync(string url);
}