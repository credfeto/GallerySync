using System.Linq;
using System.Text;

namespace Credfeto.Gallery.FileNaming
{
    public static class HashNaming
    {
        public static string PathifyHash(string pathHash)
        {
            const string separator = @"\";

            int[] indexes = {2, 4, 8, 12, 20};

            StringBuilder builder = new StringBuilder(pathHash);

            foreach (int index in indexes.OrderByDescending(keySelector: x => x))
            {
                builder.Insert(index: index, value: separator);
            }

            return builder.ToString();
        }
    }
}