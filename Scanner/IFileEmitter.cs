using System.Threading.Tasks;

namespace Twaddle.Directory.Scanner
{
    public interface IFileEmitter
    {
        Task FileFound(FileEntry entry);
    }
}