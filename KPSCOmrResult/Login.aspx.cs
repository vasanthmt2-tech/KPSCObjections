using KPSCOmrResult.DLL;
using KPSCOmrResult.Security;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI.WebControls;

namespace KPSCOmrResult
{
    /// <summary>
    /// Candidate login: registration number + examination + date of birth +
    /// captcha. The candidate comes from dbo.MarksCandidate.
    ///
    /// A date of birth is far easier to guess than an OTP, so guessing is held
    /// back three ways:
    ///  - every attempt needs a new captcha (a captcha is good for one try);
    ///  - after CandidateLoginMaxFailures wrong dates of birth in a row, the
    ///    registration number is locked for CandidateLockoutMinutes (counted in
    ///    MarksCandidate.FailedLogins / LockedUntil, so it survives restarts);
    ///  - the per-IP limit CandidateLoginPerIpPerMinute.
    /// </summary>
    public partial class Login : System.Web.UI.Page
    {
        private static readonly Regex RegNoPattern = new Regex(@"^[A-Za-z0-9]{1,10}$");

        /// <summary>One message for every mismatch, so the page does not reveal which detail was wrong.</summary>
        private const string NotVerifiedMessage =
            "The registration number, examination or date of birth is not correct.";

        private readonly SqlHelper sh = new SqlHelper();

        protected void Page_Load(object sender, EventArgs e)
        {
            ApplyTestMode();

            if (!IsPostBack)
            {
                if (AuthSession.IsCandidate(Context))
                {
                    Response.Redirect("~/Result.aspx", true);
                }

                lblMessage.Text = "";
                BindExamination();

                if (!TestMode.CaptchaDisabled)
                    RefreshCaptcha();

                // Sent here by a candidate page whose session had ended.
                if (Request.QueryString["expired"] == "1")
                    ShowError(SessionEndedMessage);
            }
        }

        /// <summary>
        /// TEMPORARY (Security\TestMode.cs): hides the captcha when it is
        /// switched off in Web.config, and shows the test mode banner.
        /// </summary>
        private void ApplyTestMode()
        {
            phCaptcha.Visible = !TestMode.CaptchaDisabled;

            string banner = TestMode.Banner(true);
            lblTestMode.Visible = banner.Length > 0;
            lblTestMode.Text = HttpUtility.HtmlEncode(banner);
        }

        // ---------------------------------------------------------------
        // Login
        // ---------------------------------------------------------------

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            if (!RequestThrottle.AllowLoginAttempt(Context, "Candidate login", "CandidateLoginPerIpPerMinute", 20))
            {
                ShowError(RequestThrottle.TooManyAttemptsMessage);
                return;
            }

            string regNo = txtRegisterNumber.Text.Trim();
            string examCode = ddlExamination.SelectedIndex > 0 ? ddlExamination.SelectedValue.Trim() : "";
            string dob;

            // The fields are checked first, so a typing mistake does not cost the
            // captcha: it stays on screen, still typed in.
            if (!RegNoPattern.IsMatch(regNo))
            {
                ShowError("Please enter a valid Registration Number.");
                return;
            }

            if (examCode.Length == 0)
            {
                ShowError("Please select the Examination.");
                return;
            }

            if (!TryParseDob(txtDob.Text, out dob))
            {
                ShowError("Please enter the Date of Birth as DD-MM-YYYY, for example 23-08-1995.");
                return;
            }

            if (!TestMode.CaptchaDisabled && !CheckCaptcha())
            {
                ShowError("Please enter the correct captcha.");
                return;
            }

            // Count the attempt before comparing, so parallel guesses cannot slip
            // past the limit. No row comes back when the registration number is
            // not registered for this examination, or is locked.
            DataTable dt = sh.ExecuteDataTable(
                @"UPDATE dbo.MarksCandidate
                  SET FailedLogins = FailedLogins + 1
                  OUTPUT inserted.RegNo, inserted.Name, inserted.Dob, inserted.FailedLogins
                  WHERE RegNo = @RegNo AND LTRIM(RTRIM(ExamCode)) = @ExamCode
                    AND (LockedUntil IS NULL OR LockedUntil <= GETDATE())",
                RegNoParam(regNo),
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });

            if (dt.Rows.Count == 0)
            {
                int minutesLeft = LockMinutesLeft(regNo, examCode);

                ShowError(minutesLeft > 0
                    ? "Too many wrong attempts for this registration number. Please try again after " +
                      minutesLeft + " minute" + (minutesLeft == 1 ? "" : "s") + "."
                    : NotVerifiedMessage);
                return;
            }

            DataRow row = dt.Rows[0];
            string registeredDob = Convert.ToString(row["Dob"]).Trim();

            if (registeredDob.Length == 0 || !string.Equals(registeredDob, dob, StringComparison.Ordinal))
            {
                RecordFailure(regNo, Convert.ToInt32(row["FailedLogins"]));
                ShowError(NotVerifiedMessage);
                return;
            }

            sh.ExecuteNonQuery(
                "UPDATE dbo.MarksCandidate SET FailedLogins = 0, LockedUntil = NULL WHERE RegNo = @RegNo",
                RegNoParam(regNo));

            ObjectionSession.LoginDetails details = new ObjectionSession.LoginDetails();
            details.RegNo = Convert.ToString(row["RegNo"]);
            details.Name = Convert.ToString(row["Name"]);
            details.Dob = registeredDob;
            details.ExamCode = examCode;

            AuthSession.SignInCandidate(Context, details);

            Response.Redirect("~/Result.aspx", true);
        }

        /// <summary>
        /// After the configured number of wrong dates of birth in a row, locks
        /// the registration number and starts the count again.
        /// </summary>
        private void RecordFailure(string regNo, int failures)
        {
            int maxFailures = RequestThrottle.Setting("CandidateLoginMaxFailures", 5);

            if (failures < maxFailures)
                return;

            int lockMinutes = RequestThrottle.Setting("CandidateLockoutMinutes", 30);

            sh.ExecuteNonQuery(
                @"UPDATE dbo.MarksCandidate
                  SET FailedLogins = 0, LockedUntil = DATEADD(MINUTE, @Minutes, GETDATE())
                  WHERE RegNo = @RegNo",
                RegNoParam(regNo),
                new SqlParameter("@Minutes", SqlDbType.Int) { Value = lockMinutes });

            AppLog.Write("WARN", "Candidate login locked for " + lockMinutes + " minutes after " + failures +
                         " wrong dates of birth: " + regNo + " (last from " + RequestThrottle.ClientIp(Context) + ")");
        }

        /// <summary>Minutes until the registration number is unlocked, or 0 when it is not locked.</summary>
        private int LockMinutesLeft(string regNo, string examCode)
        {
            object seconds = sh.ExecuteScalar(
                @"SELECT DATEDIFF(SECOND, GETDATE(), LockedUntil)
                  FROM dbo.MarksCandidate
                  WHERE RegNo = @RegNo AND LTRIM(RTRIM(ExamCode)) = @ExamCode AND LockedUntil > GETDATE()",
                RegNoParam(regNo),
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });

            if (seconds == null || seconds == DBNull.Value)
                return 0;

            // DATEDIFF counts second boundaries, so right after a lock it can give
            // one second more than the lock: never show more than the lock itself.
            int lockMinutes = RequestThrottle.Setting("CandidateLockoutMinutes", 30);

            return Math.Max(1, Math.Min(lockMinutes, (int)Math.Ceiling(Convert.ToInt32(seconds) / 60.0)));
        }

        /// <summary>
        /// Date of birth as yyyy-MM-dd, the form Upload Candidates stores. Accepts
        /// DD-MM-YYYY and DD/MM/YYYY (one-digit day and month too) and YYYY-MM-DD.
        /// </summary>
        private static bool TryParseDob(string text, out string dob)
        {
            dob = "";
            string[] formats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "yyyy-MM-dd" };
            DateTime date;

            if (!DateTime.TryParseExact((text ?? "").Trim(), formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
            {
                return false;
            }

            if (date.Year < 1900 || date > DateTime.Today)
                return false;

            dob = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        // ---------------------------------------------------------------
        // Captcha
        // ---------------------------------------------------------------

        protected void btnRefreshCaptcha_Click1(object sender, EventArgs e)
        {
            RefreshCaptcha();
        }

        /// <summary>
        /// Checks the entered captcha. Each captcha is good for one login
        /// attempt: right or wrong, a new image is shown afterwards.
        /// </summary>
        private bool CheckCaptcha()
        {
            bool retired;
            bool ok = Captcha.Check(CaptchaToken, txtCaptcha.Text, 1, out retired);

            RefreshCaptcha();

            return ok;
        }

        private void RefreshCaptcha()
        {
            txtCaptcha.Text = "";
            CaptchaToken = Captcha.NewToken();
            imgCaptcha.ImageUrl = Captcha.ImageUrl(CaptchaToken);
        }

        /// <summary>
        /// The captcha this page shows. Kept in the page's own ViewState
        /// (encrypted and signed), so loading the image URL elsewhere, or another
        /// tab, cannot change the code this page checks.
        /// </summary>
        private string CaptchaToken
        {
            get { return ViewState["CaptchaToken"] as string; }
            set { ViewState["CaptchaToken"] = value; }
        }

        // ---------------------------------------------------------------
        // Data
        // ---------------------------------------------------------------

        public void BindExamination()
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT ExamCode, ExamDescription FROM dbo.Exam WHERE IsActive = 'Y'");

            ddlExamination.Items.Clear();
            ddlExamination.DataSource = dt;
            ddlExamination.DataValueField = "ExamCode";
            ddlExamination.DataTextField = "ExamDescription";
            ddlExamination.DataBind();
            ddlExamination.Items.Insert(0, new ListItem("---SELECT---", ""));
        }

        private static SqlParameter RegNoParam(string regNo)
        {
            return new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = regNo };
        }

        // ---------------------------------------------------------------
        // Messages
        // ---------------------------------------------------------------

        /// <summary>Shown on the login pages after a session has ended.</summary>
        internal const string SessionEndedMessage =
            "Your session has ended (no activity for some time, or you signed in elsewhere). " +
            "Please log in again; anything not yet added was not saved.";

        private void ShowError(string message)
        {
            lblMessage.CssClass = "text-danger mt-3 d-block login-message";
            lblMessage.Text = HttpUtility.HtmlEncode(message);
        }
    }
}
