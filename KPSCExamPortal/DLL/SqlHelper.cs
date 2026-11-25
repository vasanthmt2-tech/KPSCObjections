using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace KPSCExamPortal.DLL
{
    /// <summary>
    /// Database access for the portal.
    ///
    /// Every method opens its own connection and closes it when done. Errors
    /// are not swallowed: they reach the caller, and unhandled ones are logged
    /// by Global.asax and shown as the error page.
    ///
    /// Prefer the overloads that take SqlParameter objects. The overloads that
    /// take parameter names and values as strings are kept for existing
    /// callers; a value containing ",@" cannot be passed through them.
    /// </summary>
    public class SqlHelper
    {
        private const int CommandTimeoutSeconds = 60;

        private static readonly string[] TextCommandKeywords =
        {
            "select", "insert", "update", "delete", "with", "set", "declare", "begin", "merge", "if"
        };

        private readonly string strConnectionString;

        public SqlHelper()
        {
            strConnectionString = ConfigurationManager.ConnectionStrings["Objection_ConnectionString"].ConnectionString;
        }

        // ---------------------------------------------------------------
        // DataTable
        // ---------------------------------------------------------------

        /// <summary>
        /// Runs a query or, when the text is not a SQL statement, a stored
        /// procedure of that name, and returns the first result set.
        /// </summary>
        public DataTable ExecuteDataTable(string query, params SqlParameter[] parameters)
        {
            DataTable dt = new DataTable();

            using (SqlConnection cnn = new SqlConnection(strConnectionString))
            using (SqlCommand cmd = CreateCommand(cnn, query, CommandTypeFor(query), parameters))
            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
            {
                try
                {
                    da.Fill(dt);
                }
                finally
                {
                    // Lets the caller reuse its SqlParameter objects.
                    cmd.Parameters.Clear();
                }
            }

            return dt;
        }

        /// <summary>
        /// String-parameter form: names "@A,@B", values "@1,@2" (each value
        /// prefixed with '@').
        /// </summary>
        public DataTable ExecuteDataTable(string Query, string ParamNames, string ParamValues)
        {
            Query = Flatten(Query);

            return ExecuteDataTable(Query, ParseParameters(ParamNames, ParamValues));
        }

        // ---------------------------------------------------------------
        // DataReader
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns an open reader. Dispose it (a using block) to close the
        /// connection.
        /// </summary>
        public SqlDataReader ExecuteDataReader(string query, params SqlParameter[] parameters)
        {
            SqlConnection cnn = new SqlConnection(strConnectionString);

            try
            {
                SqlCommand cmd = CreateCommand(cnn, query, CommandTypeFor(query), parameters);

                cnn.Open();

                return cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch
            {
                cnn.Dispose();
                throw;
            }
        }

        /// <summary>String-parameter form; see ExecuteDataTable.</summary>
        public SqlDataReader ExecuteDataReader(string query, string ParamNames, string ParamValues)
        {
            query = Flatten(query);

            return ExecuteDataReader(query, ParseParameters(ParamNames, ParamValues));
        }

        // ---------------------------------------------------------------
        // Non-query and scalar (always SQL text)
        // ---------------------------------------------------------------

        public int ExecuteNonQuery(string query, params SqlParameter[] parameters)
        {
            using (SqlConnection cnn = new SqlConnection(strConnectionString))
            using (SqlCommand cmd = CreateCommand(cnn, query, CommandType.Text, parameters))
            {
                try
                {
                    cnn.Open();
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }
        }

        public object ExecuteScalar(string query, params SqlParameter[] parameters)
        {
            using (SqlConnection cnn = new SqlConnection(strConnectionString))
            using (SqlCommand cmd = CreateCommand(cnn, query, CommandType.Text, parameters))
            {
                try
                {
                    cnn.Open();
                    return cmd.ExecuteScalar();
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static SqlCommand CreateCommand(
            SqlConnection cnn, string query, CommandType type, SqlParameter[] parameters)
        {
            SqlCommand cmd = new SqlCommand(query, cnn);

            cmd.CommandType = type;
            cmd.CommandTimeout = CommandTimeoutSeconds;

            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            return cmd;
        }

        /// <summary>
        /// Text when the first word is a SQL keyword; otherwise the text is
        /// taken to be a stored procedure name.
        /// </summary>
        private static CommandType CommandTypeFor(string query)
        {
            string text = (query ?? "").TrimStart();
            int end = 0;

            while (end < text.Length && char.IsLetter(text[end]))
                end++;

            string firstWord = text.Substring(0, end);

            // "dbo.Proc" and "Proc" stop at '.' or the end, so a keyword
            // only counts when something other than a name follows it.
            bool followedByName = end < text.Length && (text[end] == '.' || text[end] == '_');

            if (!followedByName &&
                Array.Exists(TextCommandKeywords, k => k.Equals(firstWord, StringComparison.OrdinalIgnoreCase)))
            {
                return CommandType.Text;
            }

            return CommandType.StoredProcedure;
        }

        /// <summary>Line breaks and tabs become spaces, so lines are not glued together.</summary>
        private static string Flatten(string query)
        {
            return (query ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        }

        private static SqlParameter[] ParseParameters(string paramNames, string paramValues)
        {
            string[] names = (paramNames ?? "").Replace(" ", "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            string[] values = ("," + (paramValues ?? ""))
                .Split(new[] { ",@" }, StringSplitOptions.None);

            // values[0] is the empty text before the first ",@".
            if (values.Length - 1 != names.Length)
            {
                throw new ArgumentException(
                    "Expected " + names.Length + " parameter value(s) but found " + (values.Length - 1) +
                    ". A value containing \",@\" cannot be passed in this form; use SqlParameter objects.");
            }

            SqlParameter[] parameters = new SqlParameter[names.Length];

            for (int i = 0; i < names.Length; i++)
                parameters[i] = new SqlParameter(names[i], values[i + 1]);

            return parameters;
        }
    }
}
