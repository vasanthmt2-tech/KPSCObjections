using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using System.Web.UI.WebControls;
using KPSCExamPortal.DLL;
using KPSCExamPortal.Security;

namespace KPSCExamPortal.Admin.Objections
{
    /// <summary>
    /// Admin dashboard: objections received for one examination, subject-wise,
    /// with the fee collected, payments awaiting verification, objections not
    /// yet paid for, categories, the most-objected questions and a day-wise count.
    ///
    /// "Submitted" means the fee payment succeeded (as on Candidate Objections).
    /// An objection whose payment is still being verified, or that was never
    /// paid for (or whose payment failed), is counted separately.
    ///
    /// Question numbers differ between question paper versions, so objections
    /// are mapped back to the master question through dbo.KeyAnswers (the Set
    /// column of the candidate's version). An objection that cannot be mapped is
    /// shown by its version and question number instead.
    /// </summary>
    public partial class Dashboard : KPSCExamPortal.Security.AdminPage
    {
        private static readonly CultureInfo India = new CultureInfo("en-IN");

        // Objections of the selected examination, with their payment (if any).
        private const string Base = @"
            FROM dbo.Objections o
            INNER JOIN dbo.MarksCandidate c ON c.RegNo = o.Regno AND c.ExamCode = @ExamCode
            LEFT JOIN dbo.PaymentTransactions p ON p.PaymentId = o.PaymentId";

        private const string IsSubmitted = "p.PaymentStatus = 'SUCCESS'";
        private const string IsPending = "p.PaymentStatus = 'PENDING'";
        private const string IsNotPaid = "(p.PaymentId IS NULL OR p.PaymentStatus NOT IN ('SUCCESS', 'PENDING'))";

        // The master question (and its key answer) for an objection's version
        // question number. An equality join on the examination's key map
        // (Security\KeySets.cs), not a lookup per objection: with tens of
        // thousands of objections the per-row lookup took about 30 seconds.
        private static readonly string MasterQuestion = @"
            LEFT JOIN (
                SELECT SubjectCode, VersionCode, VersionQuestionNo,
                       MasterQuestionNo AS QuestionNo, MasterAnswer
                FROM " + KeySets.MasterMapSql("@ExamCode") + @" map
            ) m
                ON m.SubjectCode = o.PaperCode
               AND m.VersionCode = o.VersionCode
               AND m.VersionQuestionNo = o.QuestionNo";

        // A key that is the same for one question across all versions.
        private const string QuestionKey =
            "COALESCE('M' + CAST(m.QuestionNo AS varchar(10)), 'V' + o.VersionCode + CAST(o.QuestionNo AS varchar(10)))";

        private readonly SqlHelper sh = new SqlHelper();

        /// <summary>Totals for the subject table footer.</summary>
        protected int TotalSubmitted, TotalCandidates, TotalQuestions, TotalPending, TotalNotPaid;

        /// <summary>Submitted objections in all categories (for the share column).</summary>
        protected int CategoryTotal;

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
            BindCategories();
            BindTopQuestions();
            BindDaily();
        }

        // ---------------------------------------------------------------
        // Summary cards
        // ---------------------------------------------------------------

        private void BindCards()
        {
            DataRow o = sh.ExecuteDataTable(
                @"SELECT
                      ISNULL(SUM(CASE WHEN " + IsSubmitted + @" THEN 1 ELSE 0 END), 0) AS Submitted,
                      COUNT(DISTINCT CASE WHEN " + IsSubmitted + @" THEN o.Regno END) AS SubmittedCandidates,
                      ISNULL(SUM(CASE WHEN " + IsPending + @" THEN 1 ELSE 0 END), 0) AS Pending,
                      ISNULL(SUM(CASE WHEN " + IsNotPaid + @" THEN 1 ELSE 0 END), 0) AS NotPaid,
                      COUNT(DISTINCT CASE WHEN " + IsNotPaid + @" THEN o.Regno END) AS NotPaidCandidates,
                      MAX(CASE WHEN " + IsSubmitted + @" THEN COALESCE(p.PaymentDate, p.VerifiedDate, p.CreatedDate) END) AS LastSubmitted
                  " + Base,
                ExamParam()).Rows[0];

            DataRow pay = sh.ExecuteDataTable(
                @"SELECT
                      ISNULL(SUM(CASE WHEN p.PaymentStatus = 'SUCCESS' THEN p.Amount END), 0) AS Collected,
                      COUNT(CASE WHEN p.PaymentStatus = 'SUCCESS' THEN 1 END) AS Successful,
                      COUNT(CASE WHEN p.PaymentStatus = 'PENDING' THEN 1 END) AS Pending,
                      COUNT(CASE WHEN p.PaymentStatus = 'FAILED' THEN 1 END) AS Failed
                  FROM dbo.PaymentTransactions p
                  INNER JOIN dbo.MarksCandidate c ON c.RegNo = p.RegNo AND c.ExamCode = @ExamCode",
                ExamParam()).Rows[0];

            int candidates = Convert.ToInt32(sh.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.MarksCandidate WHERE ExamCode = @ExamCode", ExamParam()));

            int submitted = Convert.ToInt32(o["Submitted"]);
            int submittedCandidates = Convert.ToInt32(o["SubmittedCandidates"]);

            litSubmitted.Text = Num(submitted);
            litSubmittedSub.Text = Html("by " + Plural(submittedCandidates, "candidate") +
                (o["LastSubmitted"] == DBNull.Value
                    ? ""
                    : " · last on " + Convert.ToDateTime(o["LastSubmitted"]).ToString("dd-MMM hh:mm tt", CultureInfo.InvariantCulture)));

            litCollected.Text = Money(pay["Collected"]);
            litCollectedSub.Text = Html(Plural(Convert.ToInt32(pay["Successful"]), "successful payment") +
                (Convert.ToInt32(pay["Failed"]) > 0 ? " · " + Num(pay["Failed"]) + " failed" : ""));

            litPending.Text = Num(o["Pending"]);
            litPendingSub.Text = Html("objections in " + Plural(Convert.ToInt32(pay["Pending"]), "payment") +
                " awaiting verification with SBI");

            litNotPaid.Text = Num(o["NotPaid"]);
            litNotPaidSub.Text = Html("added but not paid for, by " +
                Plural(Convert.ToInt32(o["NotPaidCandidates"]), "candidate"));

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
                         ISNULL(x.Submitted, 0) AS Submitted, ISNULL(x.Candidates, 0) AS Candidates,
                         ISNULL(x.Questions, 0) AS Questions, ISNULL(x.Pending, 0) AS Pending,
                         ISNULL(x.NotPaid, 0) AS NotPaid
                  FROM dbo.Subject s
                  OUTER APPLY (
                      SELECT SUM(CASE WHEN " + IsSubmitted + @" THEN 1 ELSE 0 END) AS Submitted,
                             COUNT(DISTINCT CASE WHEN " + IsSubmitted + @" THEN o.Regno END) AS Candidates,
                             COUNT(DISTINCT CASE WHEN " + IsSubmitted + @" THEN " + QuestionKey + @" END) AS Questions,
                             SUM(CASE WHEN " + IsPending + @" THEN 1 ELSE 0 END) AS Pending,
                             SUM(CASE WHEN " + IsNotPaid + @" THEN 1 ELSE 0 END) AS NotPaid
                      " + Base + MasterQuestion + @"
                      WHERE o.PaperCode = s.PaperCode) x
                  WHERE s.ExamCode = @ExamCode
                  ORDER BY s.PaperCode, s.SubjectCode",
                ExamParam());

            TotalSubmitted = TotalQuestions = TotalPending = TotalNotPaid = 0;

            foreach (DataRow row in dt.Rows)
            {
                TotalSubmitted += Convert.ToInt32(row["Submitted"]);
                TotalQuestions += Convert.ToInt32(row["Questions"]);
                TotalPending += Convert.ToInt32(row["Pending"]);
                TotalNotPaid += Convert.ToInt32(row["NotPaid"]);
            }

            // A candidate may object in several subjects: count each once.
            TotalCandidates = Convert.ToInt32(sh.ExecuteScalar(
                "SELECT COUNT(DISTINCT o.Regno) " + Base + " WHERE " + IsSubmitted, ExamParam()));

            rptSubjects.DataSource = dt;
            rptSubjects.DataBind();
        }

        // ---------------------------------------------------------------
        // Category-wise
        // ---------------------------------------------------------------

        private void BindCategories()
        {
            DataTable dt = sh.ExecuteDataTable(
                @"SELECT cat.CategoryCode, ISNULL(cat.CategoryDescription, CAST(cat.CategoryCode AS varchar(10))) AS CategoryDescription,
                         ISNULL(x.Objections, 0) AS Objections
                  FROM dbo.Categories cat
                  LEFT JOIN (
                      SELECT o.CategoryCode, COUNT(*) AS Objections
                      " + Base + @"
                      WHERE " + IsSubmitted + @"
                      GROUP BY o.CategoryCode) x ON x.CategoryCode = cat.CategoryCode
                  ORDER BY ISNULL(x.Objections, 0) DESC, cat.CategoryCode",
                ExamParam());

            CategoryTotal = 0;

            foreach (DataRow row in dt.Rows)
                CategoryTotal += Convert.ToInt32(row["Objections"]);

            rptCategories.DataSource = dt;
            rptCategories.DataBind();
        }

        // ---------------------------------------------------------------
        // Most-objected questions
        // ---------------------------------------------------------------

        private void BindTopQuestions()
        {
            DataTable dt = sh.ExecuteDataTable(
                @"WITH q AS (
                      SELECT o.PaperCode, " + QuestionKey + @" AS QKey,
                             m.QuestionNo AS MasterQ, o.VersionCode, o.QuestionNo AS VersionQ,
                             COALESCE(CAST(m.MasterAnswer AS varchar(10)), o.KeyAnswer) AS KeyAnswer,
                             LTRIM(RTRIM(o.ObjectionAnswer)) AS ObjectionAnswer, o.Regno
                      " + Base + MasterQuestion + @"
                      WHERE " + IsSubmitted + @"),
                  g AS (
                      SELECT PaperCode, QKey, MAX(MasterQ) AS MasterQ, MAX(VersionCode) AS VersionCode,
                             MAX(VersionQ) AS VersionQ, MAX(KeyAnswer) AS KeyAnswer,
                             COUNT(*) AS Objections, COUNT(DISTINCT Regno) AS Candidates
                      FROM q GROUP BY PaperCode, QKey),
                  a AS (
                      SELECT PaperCode, QKey, ObjectionAnswer, COUNT(*) AS N,
                             ROW_NUMBER() OVER (PARTITION BY PaperCode, QKey ORDER BY COUNT(*) DESC, ObjectionAnswer) AS Rn
                      FROM q GROUP BY PaperCode, QKey, ObjectionAnswer)
                  SELECT TOP 10 g.PaperCode, g.MasterQ, g.VersionCode, g.VersionQ, g.KeyAnswer,
                         g.Objections, g.Candidates, a.ObjectionAnswer AS TopAnswer, a.N AS TopAnswerCount
                  FROM g
                  LEFT JOIN a ON a.PaperCode = g.PaperCode AND a.QKey = g.QKey AND a.Rn = 1
                  ORDER BY g.Objections DESC, g.PaperCode, g.MasterQ, g.VersionQ",
                ExamParam());

            rptTopQuestions.DataSource = dt;
            rptTopQuestions.DataBind();
            lblNoTopQuestions.Visible = dt.Rows.Count == 0;
        }

        // ---------------------------------------------------------------
        // Day-wise
        // ---------------------------------------------------------------

        private void BindDaily()
        {
            DataTable dt = sh.ExecuteDataTable(
                @"WITH ob AS (
                      SELECT CAST(COALESCE(p.PaymentDate, p.VerifiedDate, p.CreatedDate) AS date) AS Day,
                             COUNT(*) AS Objections, COUNT(DISTINCT o.Regno) AS Candidates
                      " + Base + @"
                      WHERE " + IsSubmitted + @"
                      GROUP BY CAST(COALESCE(p.PaymentDate, p.VerifiedDate, p.CreatedDate) AS date)),
                  pay AS (
                      SELECT CAST(COALESCE(p.PaymentDate, p.VerifiedDate, p.CreatedDate) AS date) AS Day,
                             SUM(p.Amount) AS Collected
                      FROM dbo.PaymentTransactions p
                      INNER JOIN dbo.MarksCandidate c ON c.RegNo = p.RegNo AND c.ExamCode = @ExamCode
                      WHERE p.PaymentStatus = 'SUCCESS'
                      GROUP BY CAST(COALESCE(p.PaymentDate, p.VerifiedDate, p.CreatedDate) AS date))
                  SELECT ob.Day, ob.Objections, ob.Candidates, ISNULL(pay.Collected, 0) AS Collected
                  FROM ob LEFT JOIN pay ON pay.Day = ob.Day
                  ORDER BY ob.Day DESC",
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

        protected static string Money(object value)
        {
            decimal amount = value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
            return "Rs. " + amount.ToString(amount == decimal.Truncate(amount) ? "N0" : "N2", India);
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

        /// <summary>"Q. 12", or the version's numbering when the master question is not known.</summary>
        protected static string QuestionLabel(object masterQ, object versionCode, object versionQ)
        {
            if (masterQ != null && masterQ != DBNull.Value)
                return "Q. " + masterQ;

            return "Q. " + versionQ + " (version " + versionCode + ")";
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
