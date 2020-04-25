using System.Threading.Tasks;
using Images;

namespace OutputBuilderClient
{
    public interface IGalleryBuilder
    {
        public Task ProcessGalleryAsync(ISettings imageSettings);
    }
}