using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FileNaming
{
    public static class StringExtensions
    {
        private const string DateMatchRegex = @"^(19|20)\d\d([- /.])(0[1-9]|1[012])\2(0[1-9]|[12][0-9]|3[01])\b*";

        private const string NamePrefixStripCharacters = " -";

        public static string ReformatTitle(this string name, DateFormat dateFormat)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            Match match = Regex.Match(name, DateMatchRegex, RegexOptions.Singleline);
            if (match.Success)
            {
                string datePart = match.Value.Trim();

                DateTime date;
                if (DateTime.TryParse(datePart, out date))
                {
                    string fieldEnd =
                        name.Remove(0, match.Value.Length).TrimStart(NamePrefixStripCharacters.ToCharArray());
                    return string.IsNullOrEmpty(fieldEnd)
                               ? string.Format(CultureInfo.InvariantCulture, GetDateFormatString(dateFormat), date)
                               : string.Format(
                                   CultureInfo.InvariantCulture,
                                   GetDateFormatString(dateFormat) + " - {1}",
                                   date,
                                   fieldEnd);
                }
            }

            return name;
        }

        public static string ExtractDate(this string name, DateFormat dateFormat)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            Match match = Regex.Match(name, DateMatchRegex, RegexOptions.Singleline);
            if (match.Success)
            {
                string datePart = match.Value.Trim();

                DateTime date;
                if (DateTime.TryParse(datePart, out date))
                {
                    return string.Format(CultureInfo.InvariantCulture, GetDateFormatString(dateFormat), date);
                }
            }

            return string.Empty;
        }

        public static string AsEmpty(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value;
        }

        /// <summary>
        ///     Gets the date format string.
        /// </summary>
        /// <param name="dateFormat">
        ///     The date format.
        /// </param>
        /// <returns>
        ///     The date format string.
        /// </returns>
        private static string GetDateFormatString(DateFormat dateFormat)
        {
            Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));

            switch (dateFormat)
            {
                case DateFormat.ShortDate:
                    return @"{0:d MMM yyyy}";

                default:
                    return @"{0:d MMMM yyyy}";
            }
        }
    }
}