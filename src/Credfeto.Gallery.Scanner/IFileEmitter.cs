using System.Threading.Tasks;

namespace Credfeto.Gallery.Scanner;

public interface IFileEmitter
{
    Task FileFoundAsync(FileEntry entry);
}