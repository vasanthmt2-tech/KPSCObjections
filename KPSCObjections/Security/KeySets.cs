using System.Text;

namespace KPSCObjections.Security
{
    /// <summary>
    /// Question paper versions ("sets") and where their question numbers are
    /// kept in dbo.KeyAnswers.
    ///
    /// Paper P1 has sets A to L and paper P2 has sets M to X. Each paper has
    /// 12 sets, so dbo.KeyAnswers keeps 12 columns, Set1 to Set12, by
    /// position: for P1 the column Set1 holds set A, and for P2 it holds set
    /// M (Set2 holds B or N, and so on up to Set12 holding L or X).
    /// Database\migrations\005_KeyAnswerSetNumbers.sql renamed them from
    /// SetA..SetL.
    /// </summary>
    public static class KeySets
    {
        public const int Count = 12;

        private static readonly string[] Columns =
        {
            "Set1", "Set2", "Set3", "Set4", "Set5", "Set6",
            "Set7", "Set8", "Set9", "Set10", "Set11", "Set12"
        };

        /// <summary>The first set letter of a paper: A for P1, M for P2.</summary>
        public static char FirstLetter(string paperCode)
        {
            return string.Equals((paperCode ?? "").Trim(), "P2", System.StringComparison.OrdinalIgnoreCase) ? 'M' : 'A';
        }

        /// <summary>The set letters of a paper, e.g. "A to L".</summary>
        public static string Range(string paperCode)
        {
            char first = FirstLetter(paperCode);
            return first + " to " + (char)(first + Count - 1);
        }

        /// <summary>The set letter in position 0..11 of a paper.</summary>
        public static char Letter(string paperCode, int position)
        {
            return (char)(FirstLetter(paperCode) + position);
        }

        /// <summary>True when the version code is one of the paper's set letters.</summary>
        public static bool IsValid(string paperCode, string versionCode)
        {
            if (versionCode == null || versionCode.Length != 1)
                return false;

            int position = versionCode[0] - FirstLetter(paperCode);
            return position >= 0 && position < Count;
        }

        /// <summary>
        /// SQL expression for the question number of a dbo.KeyAnswers row in
        /// the version given by <paramref name="versionExpr"/>: A or M reads
        /// Set1, B or N reads Set2, and so on up to L or X reading Set12.
        /// With <paramref name="asInt"/> the value is converted to INT (NULL
        /// when it is not a number).
        /// </summary>
        public static string QuestionColumnSql(string tableAlias, string versionExpr, bool asInt)
        {
            StringBuilder sql = new StringBuilder("CASE UPPER(LTRIM(RTRIM(" + versionExpr + ")))");

            for (int i = 0; i < Count; i++)
            {
                string column = tableAlias + "." + Columns[i];

                sql.Append(" WHEN '").Append((char)('A' + i)).Append("' THEN ")
                   .Append(asInt ? "TRY_CONVERT(INT, " + column + ")" : column)
                   .Append(" WHEN '").Append((char)('M' + i)).Append("' THEN ")
                   .Append(asInt ? "TRY_CONVERT(INT, " + column + ")" : column);
            }

            return sql.Append(" END").ToString();
        }
    }
}
