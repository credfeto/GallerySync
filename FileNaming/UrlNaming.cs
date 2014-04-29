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
            string root = basePath.Trim() + "/";


            return NoHyphensNextToSlash.Replace(
                NoRepeatingHyphens.Replace(
                    AcceptableUrlCharacters.Replace(root.Replace(@"\", @"/"), Replacementchar),
                    Replacementchar),
                "/").TrimEnd(Replacementchar.ToCharArray()).ToLowerInvariant();
        }
    }
}