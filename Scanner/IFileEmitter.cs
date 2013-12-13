namespace Twaddle.Directory.Scanner
{
    public interface IFileEmitter
    {
        void FileFound(FileEntry entry);
    }
}