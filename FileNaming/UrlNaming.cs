using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FileNaming
{
    public static class UrlNaming
    {
        private const string Replacementchar = "-";

        private static readonly Regex AcceptableUrlCharacters = new Regex(@"[^\w\-/]",
                                                                          RegexOptions.Compiled |
                                                                          RegexOptions.CultureInvariant);

        private static readonly Regex NoRepeatingHyphens = new Regex(@"(\-{2,})",
                                                                     RegexOptions.Compiled |
                                                                     RegexOptions.CultureInvariant);

        private static readonly Regex NoHyphensNextToSlash = new Regex(@"(\-*/\-*)",
                                                                       RegexOptions.Compiled |
                                                                       RegexOptions.CultureInvariant);

        public static string BuildUrlSafePath(string basePath)
        {
            string root = RemoveDiacritics(basePath.Trim() + "/", true, NormaliseLWithStroke);
            root = RemoveApostrophes(root);

            return NoHyphensNextToSlash.Replace(
                NoRepeatingHyphens.Replace(
                    AcceptableUrlCharacters.Replace(root.Replace(@"\", @"/"), Replacementchar),
                    Replacementchar),
                "/").TrimEnd(Replacementchar.ToCharArray()).ToLowerInvariant();
        }

        private static string RemoveApostrophes(string root)
        {
            return root.Replace("'s", "s").Replace("'S", "S");
        }


        internal static IEnumerable<char> RemoveDiacriticsEnum(string src, bool compatNorm,
                                                               Func<char, char> customFolding)
        {
            foreach (char c in src.Normalize(compatNorm ? NormalizationForm.FormKD : NormalizationForm.FormD))
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

        internal static IEnumerable<char> RemoveDiacriticsEnum(string src, bool compatNorm)
        {
            return RemoveDiacritics(src, compatNorm, c => c);
        }

        public static string RemoveDiacritics(string src, bool compatNorm, Func<char, char> customFolding)
        {
            var sb = new StringBuilder();
            foreach (char c in RemoveDiacriticsEnum(src, compatNorm, customFolding))
                sb.Append(c);
            return sb.ToString();
        }

        public static string RemoveDiacritics(string src, bool compatNorm)
        {
            return RemoveDiacritics(src, compatNorm, c => c);
        }

        private static char NormaliseLWithStroke(char c)
        {
            switch (c)
            {
                case 'ł':
                    return 'l';
                case 'Ł':
                    return 'L';
                case 'ß':
                    return 'B';
                case 'ø':
                    return 'o';
                default:
                    return c;
            }
        }
    }
}