using System.Threading.Tasks;
using Images;

namespace OutputBuilderClient.Interfaces
{
    public interface IGalleryBuilder
    {
        public Task ProcessGalleryAsync(ISettings imageSettings);
    }
}