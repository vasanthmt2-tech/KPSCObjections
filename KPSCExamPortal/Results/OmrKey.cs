using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KPSCExamPortal.Results
{
    /// <summary>
    /// The final key and how each question is marked.
    ///
    /// A key answer is one option 1 to 4, several accepted options such as
    /// "3/4", or "GRACE" (every candidate gets the marks). A candidate's
    /// marked answer is 1 to 4, 5 (the "not attempting" circle), empty (left
    /// blank: nothing shaded) or anything else the scanner reported (for
    /// example two circles shaded).
    ///
    ///   GRACE                       +2 for everyone
    ///   5th circle                   0
    ///   left blank                  -0.5
    ///   one of the accepted answers +2
    ///   anything else               -0.5
    /// </summary>
    public static class OmrKey
    {
        public const decimal CorrectMarks = 2m;
        public const decimal WrongMarks = -0.5m;
        public const string Grace = "GRACE";
        public const string NotAttempted = "5";

        private static readonly Regex OptionList = new Regex(@"^[1-4](/[1-4])*$");

        // Words and signs that join several accepted answers: "2 or 3",
        // "2 and 3", "2 & 3", "2,3", "2/3".
        private static readonly Regex Joiner = new Regex(@"\s*(OR|AND|&|,|/)\s*");

        /// <summary>
        /// The key answer in its stored form ("2", "3/4", "GRACE"), or null
        /// when it is not a valid key answer. Several accepted answers may be
        /// written "2 or 3", "2 and 3", "2 & 3", "2,3" or "2/3", in any case and
        /// with any spaces; all become "2/3". "grace" becomes "GRACE".
        /// </summary>
        public static string Normalize(string keyAnswer)
        {
            string value = Joiner.Replace((keyAnswer ?? "").Trim().ToUpperInvariant(), "/").Replace(" ", "");

            if (value == Grace)
                return Grace;

            if (!OptionList.IsMatch(value))
                return null;

            // Each option once, in order: "4/3/4" becomes "3/4".
            return string.Join("/", value.Split('/').Distinct().OrderBy(v => v, StringComparer.Ordinal));
        }

        /// <summary>
        /// Marks for one question, or null for the 5th circle (shown as an
        /// empty cell, as no marks are given or taken).
        /// </summary>
        public static decimal? Marks(string markedAnswer, string keyAnswer)
        {
            string key = (keyAnswer ?? "").Trim().ToUpperInvariant();
            string marked = (markedAnswer ?? "").Trim();

            if (key == Grace)
                return CorrectMarks;

            if (marked == NotAttempted)
                return null;

            if (marked.Length == 0)
                return WrongMarks;

            return key.Split('/').Contains(marked) ? CorrectMarks : WrongMarks;
        }

        /// <summary>"2", "-0.5" or "" for the result popup.</summary>
        public static string Format(decimal? marks)
        {
            return marks.HasValue ? marks.Value.ToString("0.##", CultureInfo.InvariantCulture) : "";
        }
    }
}
