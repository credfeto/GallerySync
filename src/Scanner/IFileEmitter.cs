using System.Threading.Tasks;

namespace Scanner
{
    public interface IFileEmitter
    {
        Task FileFound(FileEntry entry);
    }
}