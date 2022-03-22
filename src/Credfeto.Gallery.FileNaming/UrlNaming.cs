using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Credfeto.Gallery.FileNaming;

public static class UrlNaming
{
    private const string REPLACEMENT_CHAR = "-";

    private static readonly TimeSpan RegexTimeOut = TimeSpan.FromSeconds(5);

    private static readonly Regex AcceptableUrlCharacters = new(@"[^\w\-/]", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeOut);

    private static readonly Regex NoRepeatingHyphens = new(@"(\-{2,})", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeOut);

    private static readonly Regex NoHyphensNextToSlash = new(@"(\-*/\-*)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, RegexTimeOut);


    [SuppressMessage("Microsoft.Design", "CA1055:UriReturnValuesShouldNotBeStrings", Justification = "Its a fragment")]
    public static string BuildUrlSafePath(string basePath)
    {
        string root = RemoveDiacritics(basePath.Trim() + "/", true, NormaliseLWithStroke);
        root = RemoveApostrophes(root);

        return NoHyphensNextToSlash
            .Replace(NoRepeatingHyphens.Replace(AcceptableUrlCharacters.Replace(
                    root.Replace(@"\", @"/", StringComparison.Ordinal), REPLACEMENT_CHAR), REPLACEMENT_CHAR),
                "/")
            .TrimEnd(REPLACEMENT_CHAR.ToCharArray())
            .ToLowerInvariant();
    }

    private static string RemoveApostrophes(string root)
    {
        return root.Replace("'s", "s", StringComparison.Ordinal)
            .Replace("'S", "S", StringComparison.Ordinal);
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
        return RemoveDiacritics(src, compatNorm, c => c);
    }

    public static string RemoveDiacritics(string src, bool compatNorm, Func<char, char> customFolding)
    {
        StringBuilder sb = new();

        foreach (char c in RemoveDiacriticsEnum(src, compatNorm, customFolding))
        {
            sb.Append(c);
        }

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
            case 'ł': return 'l';
            case 'Ł': return 'L';
            case 'ß': return 'B';
            case 'ø': return 'o';
            default: return c;
        }
    }
}