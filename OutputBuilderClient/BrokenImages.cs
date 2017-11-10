using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OutputBuilderClient
{
    internal static class BrokenImages
    {
        private static readonly ConcurrentDictionary<string, Exception> _brokenImages =
            new ConcurrentDictionary<string, Exception>();


        public static void LogBrokenImage(string path, Exception exception)
        {
            _brokenImages.TryAdd(path, exception);
        }

        public static string[] AllBrokenImages()
        {
            return _brokenImages.OrderBy(item => item.Key)
                .Select(FormatEntry).ToArray();
        }

        private static string FormatEntry(KeyValuePair<string, Exception> item)
        {
            return string.Join(", ",
                item.Key, 
                item.Value.Message, 
                FormatErrorSource(item.Value));
        }

        private static string FormatErrorSource(Exception item)
        {
            var method = item.TargetSite;
            if (method == null)
            {
                return "Unknown";
            }

            return string.Concat(method.DeclaringType.FullName, "::", method.Name);
        }
    }
}