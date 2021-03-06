﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Credfeto.Gallery.FileNaming
{
    public static class UrlNaming
    {
        private const string REPLACEMENT_CHAR = "-";

        private static readonly Regex AcceptableUrlCharacters = new(pattern: @"[^\w\-/]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex NoRepeatingHyphens = new(pattern: @"(\-{2,})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex NoHyphensNextToSlash = new(pattern: @"(\-*/\-*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [SuppressMessage(category: "Microsoft.Design", checkId: "CA1055:UriReturnValuesShouldNotBeStrings", Justification = "Its a fragment")]
        public static string BuildUrlSafePath(string basePath)
        {
            string root = RemoveDiacritics(basePath.Trim() + "/", compatNorm: true, customFolding: NormaliseLWithStroke);
            root = RemoveApostrophes(root);

            return NoHyphensNextToSlash
                   .Replace(NoRepeatingHyphens.Replace(AcceptableUrlCharacters.Replace(root.Replace(oldValue: @"\", newValue: @"/"), replacement: REPLACEMENT_CHAR), replacement: REPLACEMENT_CHAR),
                            replacement: "/")
                   .TrimEnd(REPLACEMENT_CHAR.ToCharArray())
                   .ToLowerInvariant();
        }

        private static string RemoveApostrophes(string root)
        {
            return root.Replace(oldValue: "'s", newValue: "s")
                       .Replace(oldValue: "'S", newValue: "S");
        }

        private static IEnumerable<char> RemoveDiacriticsEnum(string src, bool compatNorm, Func<char, char> customFolding)
        {
            foreach (char c in src.Normalize(compatNorm ? NormalizationForm.FormKD : NormalizationForm.FormD))
            {
                switch (CharUnicodeInfo.GetUnicodeCategory(c))
                {
                    case UnicodeCategory.NonSpacingMark:
                    case UnicodeCategory.SpacingCombiningMark:
                    case UnicodeCategory.EnclosingMark:

                        //do nothing
                        break;
                    default:

                        yield return customFolding(c);

                        break;
                }
            }
        }

        internal static IEnumerable<char> RemoveDiacriticsEnum(string src, bool compatNorm)
        {
            return RemoveDiacritics(src: src, compatNorm: compatNorm, customFolding: c => c);
        }

        public static string RemoveDiacritics(string src, bool compatNorm, Func<char, char> customFolding)
        {
            StringBuilder sb = new();

            foreach (char c in RemoveDiacriticsEnum(src: src, compatNorm: compatNorm, customFolding: customFolding))
            {
                sb.Append(c);
            }

            return sb.ToString();
        }

        public static string RemoveDiacritics(string src, bool compatNorm)
        {
            return RemoveDiacritics(src: src, compatNorm: compatNorm, customFolding: c => c);
        }

        private static char NormaliseLWithStroke(char c)
        {
            switch (c)
            {
                case 'ł': return 'l';
                case 'Ł': return 'L';
                case 'ß': return 'B';
                case 'ø': return 'o';
                default: return c;
            }
        }
    }
}