using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using System.Web.UI.WebControls;
using KPSCExamPortal.DLL;

namespace KPSCExamPortal.Admin.Marks
{
    /// <summary>
    /// Admin dashboard for one examination: marks objections submitted
    /// (dbo.MarksObjection), how many have a supporting document, the share
    /// of candidates who objected, the objection window, a subject-wise table
    /// and a day-wise count. There is no fee, so every row is submitted.
    /// </summary>
    public partial class Dashboard : KPSCExamPortal.Security.AdminPage
    {
        private static readonly CultureInfo India = new CultureInfo("en-IN");

        // Marks objections of the selected examination.
        private const string Base = " FROM dbo.MarksObjection m WHERE m.ExamCode = @ExamCode";

        private readonly SqlHelper sh = new SqlHelper();

        /// <summary>Totals for the subject table footer and share column.</summary>
        protected int TotalSubmitted, TotalDocuments;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindExams();
                BindDashboard();
            }
        }

        protected void ddlExam_SelectedIndexChanged(object sender, EventArgs e)
        {
            BindDashboard();
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            BindDashboard();
        }

        private string ExamCode
        {
            get { return ddlExam.SelectedValue; }
        }

        private SqlParameter ExamParam()
        {
            return new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = ExamCode };
        }

        /// <summary>
        /// All examinations; the one whose objection window is open now is
        /// selected, otherwise the first active one.
        /// </summary>
        private void BindExams()
        {
            DataTable exams = sh.ExecuteDataTable(
                @"SELECT ExamCode, ISNULL(ExamDescription, ExamCode) AS ExamDescription
                  FROM dbo.Exam
                  ORDER BY CASE WHEN (ObjectionStart IS NULL OR ObjectionStart <= GETDATE())
                                 AND (ObjectionEnd IS NULL OR ObjectionEnd >= GETDATE()) THEN 0 ELSE 1 END,
                           CASE WHEN IsActive = 'Y' THEN 0 ELSE 1 END,
                           ExamCode");

            ddlExam.DataSource = exams;
            ddlExam.DataTextField = "ExamDescription";
            ddlExam.DataValueField = "ExamCode";
            ddlExam.DataBind();

            // Choosing an examination only makes sense when there is a choice.
            pnlExam.Visible = exams.Rows.Count > 1;
        }

        private void BindDashboard()
        {
            pnlContent.Visible = ddlExam.Items.Count > 0;
            lblNoExam.Visible = !pnlContent.Visible;

            if (!pnlContent.Visible)
                return;

            lblAsOf.Text = HttpUtility.HtmlEncode("As of " + DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt", CultureInfo.InvariantCulture));
            litExamName.Text = HttpUtility.HtmlEncode(ddlExam.SelectedItem.Text);

            BindCards();
            BindSubjects();
            BindDaily();
        }

        // ---------------------------------------------------------------
        // Summary cards
        // ---------------------------------------------------------------

        private void BindCards()
        {
            DataRow o = sh.ExecuteDataTable(
                @"SELECT COUNT(*) AS Submitted,
                         COUNT(DISTINCT m.Regno) AS SubmittedCandidates,
                         SUM(CASE WHEN m.Document IS NULL THEN 0 ELSE 1 END) AS Documents,
                         MAX(m.SubmittedDate) AS LastSubmitted" + Base,
                ExamParam()).Rows[0];

            int candidates = Convert.ToInt32(sh.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.MarksCandidate WHERE ExamCode = @ExamCode", ExamParam()));

            int submitted = Convert.ToInt32(o["Submitted"]);
            int submittedCandidates = Convert.ToInt32(o["SubmittedCandidates"]);
            int documents = o["Documents"] == DBNull.Value ? 0 : Convert.ToInt32(o["Documents"]);

            litSubmitted.Text = Num(submitted);
            litSubmittedSub.Text = Html("by " + Plural(submittedCandidates, "candidate") +
                (o["LastSubmitted"] == DBNull.Value
                    ? ""
                    : " · last on " + Convert.ToDateTime(o["LastSubmitted"]).ToString("dd-MMM hh:mm tt", CultureInfo.InvariantCulture)));

            litDocuments.Text = Num(documents);
            litDocumentsSub.Text = Html(submitted == 0
                ? "no objections yet"
                : Percent(documents, submitted) + " of objections have a PDF attached");

            litCandidates.Text = Num(candidates);
            litCandidatesSub.Text = Html(candidates == 0
                ? "no candidates uploaded yet"
                : Percent(submittedCandidates, candidates) + " have submitted objections");

            BindWindow();
        }

        private void BindWindow()
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT ObjectionStart, ObjectionEnd, IsActive FROM dbo.Exam WHERE ExamCode = @ExamCode", ExamParam());

            object start = dt.Rows.Count > 0 ? dt.Rows[0]["ObjectionStart"] : DBNull.Value;
            object end = dt.Rows.Count > 0 ? dt.Rows[0]["ObjectionEnd"] : DBNull.Value;
            bool active = dt.Rows.Count > 0 && Convert.ToString(dt.Rows[0]["IsActive"]).Trim() == "Y";
            DateTime now = DateTime.Now;

            string status, detail, css;

            // Candidates cannot sign in to an inactive examination, whatever
            // its window says (Login lists only IsActive = 'Y').
            if (!active)
            {
                status = "Inactive";
                detail = "examination not active; candidates cannot sign in";
                css = "text-secondary";
            }
            else if (start != DBNull.Value && now < (DateTime)start)
            {
                status = "Not started";
                detail = "opens " + When(start);
                css = "text-secondary";
            }
            else if (end != DBNull.Value && now > (DateTime)end)
            {
                status = "Closed";
                detail = "closed " + When(end);
                css = "text-danger";
            }
            else
            {
                status = "Open";
                detail = end == DBNull.Value ? "no closing date set" : "closes " + When(end) + " (" + TimeLeft((DateTime)end - now) + " left)";
                css = "text-success";
            }

            litWindow.Text = Html(status);
            litWindowSub.Text = Html(detail);
            pnlWindowValue.CssClass = "dash-value " + css;
            lnkEditWindow.NavigateUrl = "~/Admin/Objections/ObjectionWindow.aspx?exam=" + HttpUtility.UrlEncode(ExamCode);
        }

        // ---------------------------------------------------------------
        // Subject-wise
        // ---------------------------------------------------------------

        private void BindSubjects()
        {
            DataTable dt = sh.ExecuteDataTable(
                @"SELECT s.SubjectCode, ISNULL(s.SubjectDescription, s.SubjectCode) AS SubjectDescription, s.PaperCode,
                         ISNULL(x.Submitted, 0) AS Submitted, ISNULL(x.Documents, 0) AS Documents
                  FROM dbo.Subject s
                  OUTER APPLY (
                      SELECT COUNT(*) AS Submitted,
                             SUM(CASE WHEN m.Document IS NULL THEN 0 ELSE 1 END) AS Documents
                      " + Base + @" AND m.PaperCode = s.PaperCode) x
                  WHERE s.ExamCode = @ExamCode
                  ORDER BY s.PaperCode, s.SubjectCode",
                ExamParam());

            TotalSubmitted = TotalDocuments = 0;

            foreach (DataRow row in dt.Rows)
            {
                TotalSubmitted += Convert.ToInt32(row["Submitted"]);
                TotalDocuments += Convert.ToInt32(row["Documents"]);
            }

            rptSubjects.DataSource = dt;
            rptSubjects.DataBind();
        }

        // ---------------------------------------------------------------
        // Day-wise
        // ---------------------------------------------------------------

        private void BindDaily()
        {
            DataTable dt = sh.ExecuteDataTable(
                @"SELECT CAST(m.SubmittedDate AS date) AS Day,
                         COUNT(*) AS Objections, COUNT(DISTINCT m.Regno) AS Candidates" + Base + @"
                  GROUP BY CAST(m.SubmittedDate AS date)
                  ORDER BY Day DESC",
                ExamParam());

            rptDaily.DataSource = dt;
            rptDaily.DataBind();
            lblNoDaily.Visible = dt.Rows.Count == 0;
        }

        // ---------------------------------------------------------------
        // Formatting (used by the markup)
        // ---------------------------------------------------------------

        protected static string Num(object value)
        {
            return value == null || value == DBNull.Value ? "0" : Convert.ToDecimal(value).ToString("N0", India);
        }

        protected static string Percent(object part, int whole)
        {
            if (whole <= 0)
                return "0%";

            return (Convert.ToDecimal(part) * 100m / whole).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>Bar width (0-100) for a share, as an invariant number for a style attribute.</summary>
        protected static string BarWidth(object part, int whole)
        {
            if (whole <= 0)
                return "0";

            decimal pct = Math.Min(100m, Convert.ToDecimal(part) * 100m / whole);
            return pct.ToString("0.#", CultureInfo.InvariantCulture);
        }

        protected static string Day(object value)
        {
            return Convert.ToDateTime(value).ToString("dd-MMM-yyyy (ddd)", CultureInfo.InvariantCulture);
        }

        private static string When(object value)
        {
            return Convert.ToDateTime(value).ToString("dd-MMM-yyyy hh:mm tt", CultureInfo.InvariantCulture);
        }

        private static string TimeLeft(TimeSpan left)
        {
            if (left.TotalHours >= 24)
                return Plural((int)left.TotalDays, "day") + " " + Plural(left.Hours, "hour");

            if (left.TotalMinutes >= 60)
                return Plural((int)left.TotalHours, "hour") + " " + Plural(left.Minutes, "minute");

            return Plural(Math.Max(0, (int)left.TotalMinutes), "minute");
        }

        private static string Plural(int count, string noun)
        {
            return count.ToString("N0", India) + " " + noun + (count == 1 ? "" : "s");
        }

        private static string Html(string text)
        {
            return HttpUtility.HtmlEncode(text);
        }
    }
}
