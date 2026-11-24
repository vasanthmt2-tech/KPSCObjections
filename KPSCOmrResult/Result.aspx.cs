using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Web;
using KPSCOmrResult.DLL;
using KPSCOmrResult.Results;
using KPSCOmrResult.Security;

namespace KPSCOmrResult
{
    /// <summary>
    /// The signed-in candidate's OMR result (dbo.OmrResult): one row per paper
    /// with the version, correct answers out of the questions and the marks,
    /// and a "Show Details" popup listing every question with the answer the
    /// candidate marked, the final key answer (dbo.OmrKeyAnswers) and the
    /// marks for it (Results\OmrKey.cs).
    ///
    /// The totals (correct, wrong, not attempted, left blank, marks) are the
    /// figures from the uploaded result file. The marks per question are
    /// worked out here from the key to show how they add up.
    /// </summary>
    public partial class Result : CandidatePage
    {
        private readonly SqlHelper sh = new SqlHelper();
        private ObjectionSession.LoginDetails candidate;

        protected void Page_Load(object sender, EventArgs e)
        {
            candidate = (ObjectionSession.LoginDetails)Session[AuthSession.CandidateKey];

            if (IsPostBack)
                return;

            object exam = sh.ExecuteScalar(
                "SELECT ExamDescription FROM dbo.Exam WHERE ExamCode = @ExamCode",
                ExamParam());

            litExamName.Text = Html(exam == null || exam == DBNull.Value ? "" : Convert.ToString(exam));

            string status = ConfigurationManager.AppSettings["ResultStatusText"];
            lblStatus.Text = Html(status == null ? "Provisional Result" : status);
            lblStatus.Visible = lblStatus.Text.Length > 0;

            litResults.Text = BuildResults();
        }

        private string BuildResults()
        {
            // dbo.OmrResult is in the layout of the OMR result file (Q1..Q100 as
            // columns) and has no examination column: the registration number
            // belongs to the candidate's examination.
            DataTable results = sh.ExecuteDataTable(
                @"SELECT r.*, ISNULL(s.SubjectDescription, r.Paper) AS SubjectDesc
                  FROM dbo.OmrResult r
                  LEFT JOIN dbo.Subject s ON s.ExamCode = @ExamCode AND s.PaperCode = r.Paper
                  WHERE r.RegNo = @RegNo
                  ORDER BY r.Paper",
                ExamParam(),
                new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = candidate.RegNo });

            StringBuilder html = new StringBuilder();

            html.Append("<div class=\"table-responsive\"><table class=\"table table-bordered table-striped mb-3\"><tbody>")
                .Append("<tr><td class=\"w-25\">Name</td><td>").Append(Html(candidate.Name)).Append("</td></tr>")
                .Append("<tr><td>Registration No.</td><td>").Append(Html(candidate.RegNo)).Append("</td></tr>")
                .Append("</tbody></table></div>");

            if (results.Rows.Count == 0)
            {
                html.Append("<p class=\"text-center text-muted my-3\">Your result is not available yet. Please check again later.</p>");
                return html.ToString();
            }

            StringBuilder popups = new StringBuilder();

            html.Append("<div class=\"table-responsive\"><table class=\"table table-bordered table-striped table-hover text-center result-table\">")
                .Append("<thead><tr><th>Subject Name</th><th>QP Version</th><th>Correct Answers/Total Questions</th>")
                .Append("<th>Obtained Marks</th><th></th></tr></thead><tbody>");

            foreach (DataRow r in results.Rows)
            {
                string paper = Convert.ToString(r["Paper"]).Trim();
                string version = Convert.ToString(r["Version"]).Trim();
                string[] answers = MarkedAnswers(r);
                string modalId = "details" + (paper == "P2" ? "P2" : "P1");

                html.Append("<tr><td>").Append(Html(Convert.ToString(r["SubjectDesc"])))
                    .Append("</td><td>").Append(Html(version))
                    .Append("</td><td>").Append(Convert.ToInt32(r["Correct"])).Append('/').Append(answers.Length)
                    .Append("</td><td>").Append(Html(OmrKey.Format(Convert.ToDecimal(r["MarksObt"]))))
                    .Append("</td><td><a href=\"#\" class=\"result-link\" data-bs-toggle=\"modal\" data-bs-target=\"#")
                    .Append(modalId).Append("\">Show Details</a></td></tr>");

                popups.Append(BuildPopup(modalId, r, paper, version, answers));
            }

            html.Append("</tbody></table></div>");
            html.Append(popups);

            return html.ToString();
        }

        /// <summary>The "Show Details" popup of one paper.</summary>
        private string BuildPopup(string modalId, DataRow r, string paper, string version, string[] answers)
        {
            Dictionary<int, string> key = KeyForVersion(paper, version);
            // Questions are shown in blocks of 25 side by side: 1-25, 26-50, 51-75, 76-100.
            const int rowsPerBlock = 25;
            int blocks = Math.Max(1, (answers.Length + rowsPerBlock - 1) / rowsPerBlock);
            StringBuilder html = new StringBuilder();

            html.Append("<div class=\"modal fade result-popup\" id=\"").Append(modalId)
                .Append("\" tabindex=\"-1\" aria-labelledby=\"").Append(modalId).Append("Label\" aria-hidden=\"true\">")
                .Append("<div class=\"modal-dialog modal-xl modal-dialog-scrollable\"><div class=\"modal-content\">")
                .Append("<div class=\"modal-header\"><h5 class=\"modal-title fs-6\" id=\"").Append(modalId).Append("Label\">")
                .Append("Question-wise Marks &ndash; ").Append(Html(Convert.ToString(r["SubjectDesc"])))
                .Append("<span class=\"result-popup-sub\">").Append(Html(candidate.Name))
                .Append(" &middot; Reg. No. ").Append(Html(candidate.RegNo))
                .Append(" &middot; Version ").Append(Html(version)).Append("</span></h5>")
                .Append("<button type=\"button\" class=\"btn-close\" data-bs-dismiss=\"modal\" aria-label=\"Close\"></button></div>")
                .Append("<div class=\"modal-body\">");

            if (key.Count == 0)
                html.Append("<p class=\"text-muted\">The key answers for this paper are not available yet.</p>");

            html.Append("<div class=\"table-responsive\"><table class=\"table table-bordered table-striped table-sm text-center result-details\">")
                .Append("<thead><tr>");

            for (int block = 0; block < blocks; block++)
                html.Append("<th class=\"result-qno\">Q.No</th><th>Your Answer</th><th>Key</th><th>Marks</th>");

            html.Append("</tr></thead><tbody>");

            for (int i = 0; i < rowsPerBlock && i < answers.Length; i++)
            {
                html.Append("<tr>");
                for (int block = 0; block < blocks; block++)
                    AppendQuestion(html, i + block * rowsPerBlock, answers, key);
                html.Append("</tr>");
            }

            html.Append("</tbody></table></div>");

            int notAttempted = Convert.ToInt32(r["NA"]);
            int leftBlank = Convert.ToInt32(r["LeftBlank"]);

            html.Append("<div class=\"table-responsive\"><table class=\"table table-bordered table-sm text-center\">")
                .Append("<thead><tr><th>Questions</th><th>Correct</th><th>Wrong</th>")
                .Append("<th>Not Attempted</th>")
                .Append(leftBlank > 0 ? "<th>Left Blank</th>" : "")
                .Append("<th>Total Marks</th></tr></thead><tbody><tr>")
                .Append("<td>").Append(answers.Length).Append("</td>")
                .Append("<td>").Append(Convert.ToInt32(r["Correct"])).Append("</td>")
                .Append("<td>").Append(Convert.ToInt32(r["Incorrect"])).Append("</td>")
                .Append("<td>").Append(notAttempted).Append("</td>")
                .Append(leftBlank > 0 ? "<td>" + leftBlank + "</td>" : "")
                .Append("<td>").Append(Html(OmrKey.Format(Convert.ToDecimal(r["MarksObt"])))).Append("</td>")
                .Append("</tr></tbody></table></div>");

            html.Append("<p class=\"small text-muted mb-0\">Marks: correct answer +2, wrong answer or question left blank -0.5, ")
                .Append("not attempted (5th circle) 0. GRACE: +2 for every candidate.</p>");

            html.Append("</div><div class=\"modal-footer\">")
                .Append("<button type=\"button\" class=\"btn btn-secondary\" data-bs-dismiss=\"modal\">Close</button>")
                .Append("</div></div></div></div>");

            return html.ToString();
        }

        /// <summary>The marked answers Q1..Q100 of a result row ("" for a question left blank).</summary>
        private static string[] MarkedAnswers(DataRow r)
        {
            List<string> answers = new List<string>();

            for (int q = 1; r.Table.Columns.Contains("Q" + q); q++)
                answers.Add(r["Q" + q] == DBNull.Value ? "" : Convert.ToString(r["Q" + q]).Trim());

            return answers.ToArray();
        }

        private static void AppendQuestion(StringBuilder html, int index, string[] answers, Dictionary<int, string> key)
        {
            if (index >= answers.Length)
            {
                html.Append("<td class=\"result-qno\"></td><td></td><td></td><td></td>");
                return;
            }

            int questionNo = index + 1;
            string marked = answers[index].Trim();
            string keyAnswer;
            bool hasKey = key.TryGetValue(questionNo, out keyAnswer);

            html.Append("<td class=\"result-qno\">").Append(questionNo).Append("</td>")
                .Append("<td>").Append(Html(marked)).Append("</td>")
                .Append("<td>").Append(hasKey ? Html(keyAnswer) : "-").Append("</td>")
                .Append("<td>").Append(hasKey ? Html(OmrKey.Format(OmrKey.Marks(marked, keyAnswer))) : "").Append("</td>");
        }

        /// <summary>
        /// The final key by the question numbers of the candidate's version:
        /// each master question's Set column for that version (Security\KeySets.cs)
        /// gives the question's number in that version.
        /// </summary>
        private Dictionary<int, string> KeyForVersion(string paper, string version)
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT " + KeySets.QuestionColumnSql("ka", "@Version", true) + @" AS VersionQuestionNo, ka.MasterAnswer
                  FROM dbo.OmrKeyAnswers ka
                  WHERE ka.ExamCode = @ExamCode AND ka.SubjectCode = @PaperCode",
                ExamParam(),
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 5) { Value = paper },
                new SqlParameter("@Version", SqlDbType.VarChar, 1) { Value = version });

            Dictionary<int, string> key = new Dictionary<int, string>();

            foreach (DataRow row in dt.Rows)
            {
                if (row["VersionQuestionNo"] == DBNull.Value || row["MasterAnswer"] == DBNull.Value)
                    continue;

                int q = Convert.ToInt32(row["VersionQuestionNo"]);

                if (!key.ContainsKey(q))
                    key[q] = Convert.ToString(row["MasterAnswer"]).Trim();
            }

            return key;
        }

        private SqlParameter ExamParam()
        {
            return new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = candidate.ExamCode };
        }

        private static string Html(string value)
        {
            return HttpUtility.HtmlEncode(value ?? "");
        }
    }
}
