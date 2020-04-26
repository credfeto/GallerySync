using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Credfeto.Gallery.OutputBuilder.Interfaces;

namespace Credfeto.Gallery.OutputBuilder.Services
{
    public sealed class BrokenImageTracker : IBrokenImageTracker
    {
        private static readonly ConcurrentDictionary<string, Exception> Items = new ConcurrentDictionary<string, Exception>();

        public void LogBrokenImage(string path, Exception exception)
        {
            Items.TryAdd(path, exception);
        }

        public string[] AllBrokenImages()
        {
            return Items.OrderBy(keySelector: item => item.Key)
                        .Select(FormatEntry)
                        .ToArray();
        }

        private static string FormatEntry(KeyValuePair<string, Exception> item)
        {
            return string.Join(separator: ", ", item.Key, item.Value.Message, FormatErrorSource(item.Value));
        }

        private static string FormatErrorSource(Exception item)
        {
            MethodBase method = item.TargetSite;

            if (method == null)
            {
                return "Unknown";
            }

            return string.Concat(method.DeclaringType.FullName, str1: "::", method.Name);
        }
    }
}