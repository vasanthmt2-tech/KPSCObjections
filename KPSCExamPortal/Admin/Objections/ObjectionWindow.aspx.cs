using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using System.Web.UI.WebControls;
using KPSCExamPortal.DLL;
using KPSCExamPortal.Payments;
using KPSCExamPortal.Security;

namespace KPSCExamPortal.Admin.Objections
{
    /// <summary>
    /// Admin page to view and change the two objection windows of an examination,
    /// one card each:
    ///   - answer key objections: dbo.Exam.ObjectionStart / ObjectionEnd;
    ///   - marks objections:      dbo.Exam.MarksObjectionStart / MarksObjectionEnd.
    /// Candidates can file only while the window of that kind is open; windows are
    /// read from the database on every request, so a change applies at once.
    ///
    /// Times are the database server's clock (as used by ObjectionRules). Each card
    /// saves only its own window, and only if nobody else changed that window since
    /// the page was loaded. Every change is recorded in the audit trail.
    /// The answer key card is hidden while that module is switched off (PortalModules).
    /// </summary>
    public partial class EditObjectionWindow : AdminPage
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimeFormat = "HH:mm";
        private const string ShowFormat = "dd-MMM-yyyy hh:mm tt";

        private readonly SqlHelper sh = new SqlHelper();

        /// <summary>The controls of one card.</summary>
        private class WindowCard
        {
            public ObjectionKind Kind;
            public string Name;
            public TextBox StartDate, StartTime, EndDate, EndTime;
            public Label Status, Current;
        }

        private WindowCard Card(ObjectionKind kind)
        {
            return kind == ObjectionKind.Marks
                ? new WindowCard
                {
                    Kind = kind, Name = "marks objection",
                    StartDate = txtMarksStartDate, StartTime = txtMarksStartTime,
                    EndDate = txtMarksEndDate, EndTime = txtMarksEndTime,
                    Status = lblMarksStatus, Current = lblMarksCurrent
                }
                : new WindowCard
                {
                    Kind = kind, Name = "answer key objection",
                    StartDate = txtKeyStartDate, StartTime = txtKeyStartTime,
                    EndDate = txtKeyEndDate, EndTime = txtKeyEndTime,
                    Status = lblKeyStatus, Current = lblKeyCurrent
                };
        }

        /// <summary>A window as loaded, to detect a change made meanwhile by someone else.</summary>
        private DateTime? Loaded(ObjectionKind kind, string part)
        {
            return ViewState["Loaded" + kind + part] as DateTime?;
        }

        private void SetLoaded(ObjectionKind kind, string part, DateTime? value)
        {
            ViewState["Loaded" + kind + part] = value;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            pnlKey.Visible = KPSCExamPortal.PortalModules.KeyAnswerObjections;

            if (!IsPostBack)
            {
                BindExams();
                LoadWindows();
            }
        }

        protected void ddlExam_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowMessage("", true);
            LoadWindows();
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            ShowMessage("", true);
            LoadWindows();
        }

        protected void btnSaveKey_Click(object sender, EventArgs e)
        {
            // The card is hidden while the module is off; refuse a save posted anyway.
            if (KPSCExamPortal.PortalModules.KeyAnswerObjections)
                Save(Card(ObjectionKind.KeyAnswer));
        }

        protected void btnSaveMarks_Click(object sender, EventArgs e)
        {
            Save(Card(ObjectionKind.Marks));
        }

        private void Save(WindowCard card)
        {
            if (ddlExam.Items.Count == 0)
                return;

            string title = Capitalise(card.Name);
            DateTime start, end;

            if (!TryReadDateTime(card.StartDate.Text, card.StartTime.Text, out start))
            {
                ShowMessage(title + " window: please enter a valid start date and time.", false);
                return;
            }

            if (!TryReadDateTime(card.EndDate.Text, card.EndTime.Text, out end))
            {
                ShowMessage(title + " window: please enter a valid end date and time.", false);
                return;
            }

            if (end <= start)
            {
                ShowMessage(title + " window: the end must be after the start.", false);
                return;
            }

            // The end is taken to the last second of its minute (23:59 means 23:59:59).
            end = end.AddSeconds(59);

            string examCode = ddlExam.SelectedValue;
            DateTime? oldStart = Loaded(card.Kind, "Start"), oldEnd = Loaded(card.Kind, "End");

            // Column names come from ObjectionRules only, never from input.
            string startCol = ObjectionRules.StartColumn(card.Kind), endCol = ObjectionRules.EndColumn(card.Kind);

            int updated = sh.ExecuteNonQuery(
                "UPDATE dbo.Exam SET " + startCol + " = @Start, " + endCol + " = @End " +
                "WHERE ExamCode = @ExamCode " +
                "AND ((" + startCol + " IS NULL AND @OldStart IS NULL) OR " + startCol + " = @OldStart) " +
                "AND ((" + endCol + " IS NULL AND @OldEnd IS NULL) OR " + endCol + " = @OldEnd)",
                new SqlParameter("@Start", SqlDbType.DateTime2) { Value = start },
                new SqlParameter("@End", SqlDbType.DateTime2) { Value = end },
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode },
                new SqlParameter("@OldStart", SqlDbType.DateTime2) { Value = (object)oldStart ?? DBNull.Value },
                new SqlParameter("@OldEnd", SqlDbType.DateTime2) { Value = (object)oldEnd ?? DBNull.Value });

            if (updated == 0)
            {
                LoadWindows();
                ShowMessage("The " + card.Name + " window was changed by someone else meanwhile. " +
                            "The current values are shown below; please check them and save again.", false);
                return;
            }

            // Recorded in the audit trail (and the application log).
            AdminAudit.Record(Context, AdminAudit.ChangeObjectionWindow,
                "Exam " + examCode + " (" + (card.Kind == ObjectionKind.Marks ? "Marks" : "Answer key") + "): from " +
                Show(oldStart) + " - " + Show(oldEnd) + " to " + Show(start) + " - " + Show(end));

            LoadWindows();
            ShowMessage("The " + card.Name + " window has been saved. Candidates see the change at once.", true);
        }

        private void BindExams()
        {
            DataTable exams = sh.ExecuteDataTable(
                @"SELECT ExamCode, ISNULL(ExamDescription, ExamCode) AS ExamDescription
                  FROM dbo.Exam
                  ORDER BY CASE WHEN IsActive = 'Y' THEN 0 ELSE 1 END, ExamCode");

            ddlExam.DataSource = exams;
            ddlExam.DataTextField = "ExamDescription";
            ddlExam.DataValueField = "ExamCode";
            ddlExam.DataBind();

            // Opened from a dashboard for a particular examination.
            string requested = Request.QueryString["exam"];

            if (!string.IsNullOrEmpty(requested) && ddlExam.Items.FindByValue(requested) != null)
                ddlExam.SelectedValue = requested;

            pnlForm.Visible = exams.Rows.Count > 0;
            lblNoExam.Visible = exams.Rows.Count == 0;
        }

        private void LoadWindows()
        {
            if (ddlExam.Items.Count == 0)
                return;

            DataTable dt = sh.ExecuteDataTable(
                @"SELECT ObjectionStart, ObjectionEnd, MarksObjectionStart, MarksObjectionEnd, IsActive, GETDATE() AS Now
                  FROM dbo.Exam WHERE ExamCode = @ExamCode",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = ddlExam.SelectedValue });

            if (dt.Rows.Count == 0)
                return;

            DataRow row = dt.Rows[0];
            DateTime now = Convert.ToDateTime(row["Now"]);
            bool active = Convert.ToString(row["IsActive"]).Trim() == "Y";

            lblNow.Text = HttpUtility.HtmlEncode(now.ToString(ShowFormat, CultureInfo.InvariantCulture));

            foreach (ObjectionKind kind in new[] { ObjectionKind.KeyAnswer, ObjectionKind.Marks })
                ShowWindow(Card(kind), row, now, active);
        }

        private void ShowWindow(WindowCard card, DataRow row, DateTime now, bool active)
        {
            DateTime? start = DateOrNull(row[ObjectionRules.StartColumn(card.Kind)]);
            DateTime? end = DateOrNull(row[ObjectionRules.EndColumn(card.Kind)]);

            SetLoaded(card.Kind, "Start", start);
            SetLoaded(card.Kind, "End", end);

            card.StartDate.Text = start.HasValue ? start.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : "";
            card.StartTime.Text = start.HasValue ? start.Value.ToString(TimeFormat, CultureInfo.InvariantCulture) : "";
            card.EndDate.Text = end.HasValue ? end.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : "";
            card.EndTime.Text = end.HasValue ? end.Value.ToString(TimeFormat, CultureInfo.InvariantCulture) : "";

            string status, css;

            if (!active)
            {
                status = "Closed (examination not active)";
                css = "badge text-bg-secondary";
            }
            else if (start.HasValue && now < start.Value)
            {
                status = "Not started";
                css = "badge text-bg-secondary";
            }
            else if (end.HasValue && now > end.Value)
            {
                status = "Closed";
                css = "badge text-bg-danger";
            }
            else
            {
                status = "Open";
                css = "badge text-bg-success";
            }

            card.Status.Text = HttpUtility.HtmlEncode(status);
            card.Status.CssClass = css;
            card.Current.Text = HttpUtility.HtmlEncode(Show(start) + "  to  " + Show(end));
        }

        private static DateTime? DateOrNull(object value)
        {
            return value == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(value);
        }

        private static bool TryReadDateTime(string date, string time, out DateTime value)
        {
            value = DateTime.MinValue;

            DateTime d, t;

            if (!DateTime.TryParseExact((date ?? "").Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return false;

            if (!DateTime.TryParseExact((time ?? "").Trim(), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                return false;

            if (d.Year < 2000 || d.Year > 2100)
                return false;

            value = d.Date.Add(t.TimeOfDay);
            return true;
        }

        private static string Show(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString(ShowFormat, CultureInfo.InvariantCulture) : "(not set)";
        }

        private static string Capitalise(string text)
        {
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private void ShowMessage(string message, bool ok)
        {
            lblMessage.Text = HttpUtility.HtmlEncode(message);
            lblMessage.CssClass = message.Length == 0 ? "" : (ok ? "alert alert-success d-block" : "alert alert-danger d-block");
        }
    }
}
