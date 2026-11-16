using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using KPSCObjections.DLL;
using KPSCObjections.Security;

namespace KPSCObjections
{
    /// <summary>
    /// Admin page to view and change the objection window of an examination
    /// (dbo.Exam.ObjectionStart / ObjectionEnd). Candidates can add objections
    /// and pay only while the window is open; the window is read from the
    /// database on every request, so a change applies at once.
    ///
    /// Times are the database server's clock (as used by ObjectionRules).
    /// A save only succeeds if nobody else changed the window since the page
    /// was loaded, and every change is written to the application log.
    /// </summary>
    public partial class EditObjectionWindow : AdminPage
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimeFormat = "HH:mm";
        private const string ShowFormat = "dd-MMM-yyyy hh:mm tt";

        private readonly SqlHelper sh = new SqlHelper();

        /// <summary>The window as loaded, to detect a change made meanwhile by someone else.</summary>
        private DateTime? LoadedStart
        {
            get { return ViewState["LoadedStart"] as DateTime?; }
            set { ViewState["LoadedStart"] = value; }
        }

        private DateTime? LoadedEnd
        {
            get { return ViewState["LoadedEnd"] as DateTime?; }
            set { ViewState["LoadedEnd"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindExams();
                LoadWindow();
            }
        }

        protected void ddlExam_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowMessage("", true);
            LoadWindow();
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            ShowMessage("", true);
            LoadWindow();
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (ddlExam.Items.Count == 0)
                return;

            DateTime start, end;

            if (!TryReadDateTime(txtStartDate.Text, txtStartTime.Text, out start))
            {
                ShowMessage("Please enter a valid start date and time.", false);
                return;
            }

            if (!TryReadDateTime(txtEndDate.Text, txtEndTime.Text, out end))
            {
                ShowMessage("Please enter a valid end date and time.", false);
                return;
            }

            if (end <= start)
            {
                ShowMessage("The end must be after the start.", false);
                return;
            }

            // The end is taken to the last second of its minute (23:59 means 23:59:59).
            end = end.AddSeconds(59);

            string examCode = ddlExam.SelectedValue;

            int updated = sh.ExecuteNonQuery(
                @"UPDATE dbo.Exam
                  SET ObjectionStart = @Start, ObjectionEnd = @End
                  WHERE ExamCode = @ExamCode
                    AND ((ObjectionStart IS NULL AND @OldStart IS NULL) OR ObjectionStart = @OldStart)
                    AND ((ObjectionEnd IS NULL AND @OldEnd IS NULL) OR ObjectionEnd = @OldEnd)",
                new SqlParameter("@Start", SqlDbType.DateTime2) { Value = start },
                new SqlParameter("@End", SqlDbType.DateTime2) { Value = end },
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode },
                new SqlParameter("@OldStart", SqlDbType.DateTime2) { Value = (object)LoadedStart ?? DBNull.Value },
                new SqlParameter("@OldEnd", SqlDbType.DateTime2) { Value = (object)LoadedEnd ?? DBNull.Value });

            if (updated == 0)
            {
                LoadWindow();
                ShowMessage("The objection window was changed by someone else meanwhile. " +
                            "The current values are shown below; please check them and save again.", false);
                return;
            }

            // Recorded in the audit trail (and the application log).
            AdminAudit.Record(Context, AdminAudit.ChangeObjectionWindow,
                "Exam " + examCode + ": from " + Show(LoadedStart) + " - " + Show(LoadedEnd) +
                " to " + Show(start) + " - " + Show(end));

            LoadWindow();
            ShowMessage("The objection window has been saved. Candidates see the change at once.", true);
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

            // Opened from the dashboard for a particular examination.
            string requested = Request.QueryString["exam"];

            if (!string.IsNullOrEmpty(requested) && ddlExam.Items.FindByValue(requested) != null)
                ddlExam.SelectedValue = requested;

            pnlForm.Visible = exams.Rows.Count > 0;
            lblNoExam.Visible = exams.Rows.Count == 0;
        }

        private void LoadWindow()
        {
            if (ddlExam.Items.Count == 0)
                return;

            DataTable dt = sh.ExecuteDataTable(
                @"SELECT ObjectionStart, ObjectionEnd, IsActive, GETDATE() AS Now
                  FROM dbo.Exam WHERE ExamCode = @ExamCode",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = ddlExam.SelectedValue });

            if (dt.Rows.Count == 0)
                return;

            DataRow row = dt.Rows[0];
            DateTime? start = row["ObjectionStart"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ObjectionStart"]);
            DateTime? end = row["ObjectionEnd"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ObjectionEnd"]);
            DateTime now = Convert.ToDateTime(row["Now"]);
            bool active = Convert.ToString(row["IsActive"]).Trim() == "Y";

            LoadedStart = start;
            LoadedEnd = end;

            txtStartDate.Text = start.HasValue ? start.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : "";
            txtStartTime.Text = start.HasValue ? start.Value.ToString(TimeFormat, CultureInfo.InvariantCulture) : "";
            txtEndDate.Text = end.HasValue ? end.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : "";
            txtEndTime.Text = end.HasValue ? end.Value.ToString(TimeFormat, CultureInfo.InvariantCulture) : "";

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

            lblStatus.Text = HttpUtility.HtmlEncode(status);
            lblStatus.CssClass = css;
            lblCurrent.Text = HttpUtility.HtmlEncode(Show(start) + "  to  " + Show(end));
            lblNow.Text = HttpUtility.HtmlEncode(now.ToString(ShowFormat, CultureInfo.InvariantCulture));
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

        private void ShowMessage(string message, bool ok)
        {
            lblMessage.Text = HttpUtility.HtmlEncode(message);
            lblMessage.CssClass = message.Length == 0 ? "" : (ok ? "alert alert-success d-block" : "alert alert-danger d-block");
        }
    }
}
