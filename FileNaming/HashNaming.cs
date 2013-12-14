using System.Linq;
using System.Text;

namespace FileNaming
{
    public static class HashNaming
    {
        public static string PathifyHash(string pathHash)
        {
            const string separator = @"\";

            var indexes = new[] {2, 4, 8, 12, 20 };

            var builder = new StringBuilder(pathHash);

            foreach (var index in indexes.OrderByDescending( x => x ))
            {
                builder.Insert(index, separator);
            }

            return builder.ToString();
        }
    }
}