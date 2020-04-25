using System.Threading.Tasks;

namespace Scanner
{
    public interface IFileEmitter
    {
        Task FileFoundAsync(FileEntry entry);
    }
}