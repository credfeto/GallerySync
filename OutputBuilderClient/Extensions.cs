using System.Collections.Generic;
using System.Linq;

namespace OutputBuilderClient
{
    internal static class Extensions
    {
        public static bool HasAny<T>(this IEnumerable<T> source)
        {
            return source != null && source.Any();
        }
    }
}