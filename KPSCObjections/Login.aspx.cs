using KPSCObjections.DLL;
using KPSCObjections.Security;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI.WebControls;

namespace KPSCObjections
{
    /// <summary>
    /// Candidate login.
    ///   1. Registration number + examination + captcha  -> Get OTP (SMS).
    ///   2. OTP                                           -> Login.
    /// The captcha guards the SMS send; the OTP (5 attempts, 5 minutes) guards the login.
    /// </summary>
    public partial class Login : System.Web.UI.Page
    {
        private const string OtpRegNoKey = "OTPRegNo";
        private const string OtpExamKey = "OTPExamCode";
        private const string OtpRequestedAtKey = "OTPRequestedAtUtc";

        private const int MaxOtpAttempts = 5;
        private const int ResendWaitSeconds = 60;
        private const int SmsTimeoutMs = 30000;

        private static readonly Regex RegNoPattern = new Regex(@"^[A-Za-z0-9]{1,10}$");
        private static readonly Regex OtpPattern = new Regex(@"^\d{6}$");
        private static readonly Regex MobilePattern = new Regex(@"^\d{10}$");

        private readonly SqlHelper sh = new SqlHelper();

        private enum OtpCheck
        {
            Ok,
            NotFound,
            Used,
            Expired,
            TooManyAttempts,
            Wrong
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (AuthSession.IsCandidate(Context))
                {
                    Response.Redirect("~/Objection.aspx", true);
                }

                lblMessage.Text = "";
                BindExamination();
                RefreshCaptcha();
            }
        }

        // ---------------------------------------------------------------
        // Step 1: send the OTP
        // ---------------------------------------------------------------

        protected void btnGetOTP_Click(object sender, EventArgs e)
        {
            RequestOtp();
        }

        protected void btnResendOTP_Click(object sender, EventArgs e)
        {
            // The button is disabled until the wait is over; this also stops a
            // resend posted early by other means.
            int wait = SecondsUntilResend();

            if (wait > 0)
            {
                // The seconds left are added by the countdown (OnPreRender).
                ShowError("An OTP was sent recently and is still valid.");
                return;
            }

            RequestOtp();
        }

        /// <summary>Seconds left before Resend OTP may be used in this session (0 = now).</summary>
        private int SecondsUntilResend()
        {
            if (!(Session[OtpRequestedAtKey] is DateTime))
                return 0;

            double elapsed = (DateTime.UtcNow - (DateTime)Session[OtpRequestedAtKey]).TotalSeconds;

            return Math.Max(0, (int)Math.Ceiling(ResendWaitSeconds - elapsed));
        }

        /// <summary>
        /// Resend OTP is disabled until an OTP has been requested and 60 seconds
        /// have passed; the seconds left go to the message label, where
        /// Scripts/login-otp.js counts down and enables the button.
        ///
        /// Disabled with the HTML attribute, not Enabled = false: a control
        /// disabled on the server is left out of event validation, so the click
        /// after the script re-enables it would be rejected as tampering.
        /// </summary>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            bool otpRequested = Session[OtpRegNoKey] is string;
            int wait = SecondsUntilResend();

            if (!otpRequested || wait > 0)
                btnResendOTP.Attributes["disabled"] = "disabled";
            else
                btnResendOTP.Attributes.Remove("disabled");

            // No countdown under an "OTP limit reached" message.
            if (otpRequested && wait > 0 && !smsCapReached)
                lblMessage.Attributes["data-resend-seconds"] = wait.ToString();
            else
                lblMessage.Attributes.Remove("data-resend-seconds");
        }

        private void RequestOtp()
        {
            // Per-IP limit on OTP requests (Get OTP and Resend OTP together),
            // checked before the captcha is used up.
            if (!RequestThrottle.AllowLoginAttempt(Context, "OTP request", "LoginOtpRequestsPerIpPerMinute", 20))
            {
                ShowError(RequestThrottle.TooManyAttemptsMessage);
                return;
            }

            string regNo = txtRegisterNumber.Text.Trim();
            string examCode = ddlExamination.SelectedIndex > 0
                ? ddlExamination.SelectedValue.Trim()
                : "";

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

            if (!CheckCaptcha())
            {
                ShowError("Please enter the correct captcha.");
                return;
            }

            // Caps on SMS volume (cost), on top of the per-IP limit and the
            // 60-second wait per registration number.
            if (!WithinSmsCaps(regNo))
                return;

            // From here on every outcome shows the same message, so the page does
            // not reveal which registration numbers exist (user enumeration).
            // The OTP step is tied to what was entered either way; for details
            // that do not match, no OTP exists and the login step fails exactly
            // like a wrong OTP.
            Session[OtpRegNoKey] = regNo;
            Session[OtpExamKey] = examCode;
            Session[OtpRequestedAtKey] = DateTime.UtcNow;
            txtOTP.Text = "";

            string mobile = GetRegisteredMobile(regNo, examCode);

            if (mobile == null)
            {
                ShowSuccess(OtpRequestedMessage);
                return;
            }

            if (!MobilePattern.IsMatch(mobile))
            {
                AppLog.Write("WARN", "OTP not sent: no valid mobile number registered for " + regNo);
                ShowSuccess(OtpRequestedMessage);
                return;
            }

            if (SecondsSinceLastSend(regNo) < ResendWaitSeconds)
            {
                // The OTP sent less than a minute ago is still valid.
                ShowSuccess(OtpRequestedMessage);
                return;
            }

            string otp = GenerateOTP();

            if (!SaveOTP(regNo, otp))
            {
                ShowError("Unable to generate OTP. Please try again later.");
                return;
            }

            bool firstOverCap;
            RequestThrottle.Allow(SmsHourBucket, "all", int.MaxValue, TimeSpan.FromHours(1), out firstOverCap);

            if (!SendSMS(mobile, otp))
            {
                InvalidateOTP(regNo);
                ShowError("Unable to send OTP. Please try again later.");
                return;
            }

            ShowSuccess(OtpRequestedMessage);
        }

        /// <summary>Set when this request was refused by an SMS cap.</summary>
        private bool smsCapReached;

        private const string SmsHourBucket = "otp-sms-hour";
        private const string RegNoDayBucket = "otp-requests-day";

        /// <summary>
        /// False (with a message shown) when:
        ///  - OTP requests for this registration number today reached
        ///    OtpRequestsPerRegNoPerDay. Counted for every request, whether
        ///    or not the number is registered, so the message does not reveal
        ///    which numbers exist;
        ///  - OTP SMS sent by the portal in the current hour reached
        ///    OtpSmsPerHour (a ceiling on cost if the other limits are beaten).
        /// Both counters are kept in memory and start again when the
        /// application restarts.
        /// </summary>
        private bool WithinSmsCaps(string regNo)
        {
            int perHour = RequestThrottle.Setting("OtpSmsPerHour", 2000);

            if (RequestThrottle.Count(SmsHourBucket, "all") >= perHour)
            {
                if (RequestThrottle.Count(SmsHourBucket, "all-refused") == 0)
                {
                    AppLog.Write("WARN", "OTP SMS limit of " + perHour + " an hour reached; " +
                                 "OTP requests are refused until the hour is over.");
                }

                bool ignored;
                RequestThrottle.Allow(SmsHourBucket, "all-refused", int.MaxValue, TimeSpan.FromHours(1), out ignored);

                smsCapReached = true;
                ShowError("The OTP service is busy. Please try again after some time.");
                return false;
            }

            int perDay = RequestThrottle.Setting("OtpRequestsPerRegNoPerDay", 10);
            bool firstRejection;

            if (!RequestThrottle.Allow(RegNoDayBucket, regNo.ToUpperInvariant(), perDay, TimeSpan.FromDays(1), out firstRejection))
            {
                if (firstRejection)
                {
                    AppLog.Write("WARN", "OTP requests for registration number " + regNo + " reached " + perDay +
                                 " in a day (last from " + RequestThrottle.ClientIp(Context) + "); further ones refused.");
                }

                smsCapReached = true;
                ShowError("Too many OTP requests for this registration number today. Please try again tomorrow.");
                return false;
            }

            return true;
        }

        /// <summary>Shown for every OTP request, whether or not the details matched.</summary>
        private static readonly string OtpRequestedMessage =
            "An OTP has been sent to the " +
            "registered mobile number. It is valid for 5 minutes.";

        // ---------------------------------------------------------------
        // Step 2: verify the OTP and sign in
        // ---------------------------------------------------------------

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            // Per-IP limit on OTP login attempts (on top of 5 attempts per OTP).
            if (!RequestThrottle.AllowLoginAttempt(Context, "OTP login", "LoginOtpChecksPerIpPerMinute", 20))
            {
                ShowError(RequestThrottle.TooManyAttemptsMessage);
                return;
            }

            string regNo = Session[OtpRegNoKey] as string;
            string examCode = Session[OtpExamKey] as string;

            if (string.IsNullOrEmpty(regNo) || string.IsNullOrEmpty(examCode))
            {
                ShowError("Please request an OTP first.");
                return;
            }

            string enteredOTP = txtOTP.Text.Trim();

            if (!OtpPattern.IsMatch(enteredOTP))
            {
                ShowError("Please enter a valid 6-digit OTP.");
                return;
            }

            // One message for every failure: telling "wrong" from "not found" or
            // "expired" would reveal which registration numbers have an OTP and so
            // exist. The reason is still enforced (5 attempts, 5 minutes, single use).
            if (CheckOtp(regNo, enteredOTP) != OtpCheck.Ok)
            {
                ShowError("Invalid or expired OTP. Please try again, or request a new OTP " +
                          "(an OTP is valid for 5 minutes and allows 5 attempts).");
                return;
            }

            DataTable dt = sh.ExecuteDataTable(
                "SELECT RegNo, Name, Dob FROM dbo.Candidate " +
                "WHERE RegNo = @RegNo AND LTRIM(RTRIM(ExamCode)) = @ExamCode",
                new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = regNo },
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });

            if (dt.Rows.Count == 0)
            {
                ShowError("Candidate details could not be verified.");
                return;
            }

            ObjectionSession.LoginDetails details = new ObjectionSession.LoginDetails();
            details.RegNo = Convert.ToString(dt.Rows[0]["RegNo"]);
            details.Name = Convert.ToString(dt.Rows[0]["Name"]);
            details.Dob = Convert.ToString(dt.Rows[0]["Dob"]);
            details.ExamCode = examCode;

            AuthSession.SignInCandidate(Context, details);

            Response.Redirect("~/Objection.aspx", true);
        }

        private OtpCheck CheckOtp(string regNo, string enteredOTP)
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT OTPHash, OTPSalt, Attempts, IsUsed, " +
                "CASE WHEN ExpiresAt < GETDATE() THEN 1 ELSE 0 END AS IsExpired " +
                "FROM dbo.CandidateOTP WHERE RegNo = @RegNo",
                RegNoParam(regNo));

            if (dt.Rows.Count == 0)
                return OtpCheck.NotFound;

            DataRow row = dt.Rows[0];

            if (Convert.ToBoolean(row["IsUsed"]))
                return OtpCheck.Used;

            if (Convert.ToInt32(row["IsExpired"]) == 1)
            {
                InvalidateOTP(regNo);
                return OtpCheck.Expired;
            }

            if (Convert.ToInt32(row["Attempts"]) >= MaxOtpAttempts)
            {
                InvalidateOTP(regNo);
                return OtpCheck.TooManyAttempts;
            }

            // Count the attempt before comparing, so parallel guesses cannot
            // slip past the limit.
            sh.ExecuteNonQuery(
                "UPDATE dbo.CandidateOTP SET Attempts = Attempts + 1 WHERE RegNo = @RegNo AND IsUsed = 0",
                RegNoParam(regNo));

            byte[] enteredHash = HashOTP(enteredOTP, (byte[])row["OTPSalt"]);

            if (!PasswordHasher.FixedTimeEquals((byte[])row["OTPHash"], enteredHash))
                return OtpCheck.Wrong;

            // Only one request can flip IsUsed from 0 to 1, so an OTP logs in once.
            int claimed = sh.ExecuteNonQuery(
                "UPDATE dbo.CandidateOTP SET IsUsed = 1 WHERE RegNo = @RegNo AND IsUsed = 0",
                RegNoParam(regNo));

            return claimed == 1 ? OtpCheck.Ok : OtpCheck.Used;
        }

        // ---------------------------------------------------------------
        // Captcha
        // ---------------------------------------------------------------

        protected void btnRefreshCaptcha_Click1(object sender, EventArgs e)
        {
            RefreshCaptcha();
        }

        /// <summary>
        /// A correct captcha stays on screen and serves up to this many OTP
        /// requests (Get OTP and Resends) before a new one is shown. SMS volume is
        /// still limited by the 60-second wait per registration number and the
        /// per-IP limit.
        /// </summary>
        private const int CaptchaUsesPerCode = 3;

        /// <summary>
        /// Checks the entered captcha. A wrong one is replaced by a new image; a
        /// correct one stays on screen until it has been used CaptchaUsesPerCode times.
        /// </summary>
        private bool CheckCaptcha()
        {
            bool retired;
            bool ok = Captcha.Check(CaptchaToken, txtCaptcha.Text, CaptchaUsesPerCode, out retired);

            if (retired)
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

        /// <summary>Mobile number of the candidate, or null when the
        /// registration number is not registered for that examination.</summary>
        private string GetRegisteredMobile(string regNo, string examCode)
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT MobileNo FROM dbo.Candidate " +
                "WHERE RegNo = @RegNo AND LTRIM(RTRIM(ExamCode)) = @ExamCode",
                new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = regNo },
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });

            if (dt.Rows.Count == 0)
                return null;

            return Convert.ToString(dt.Rows[0][0]).Trim();
        }

        private int SecondsSinceLastSend(string regNo)
        {
            object seconds = sh.ExecuteScalar(
                "SELECT DATEDIFF(SECOND, LastSentAt, GETDATE()) FROM dbo.CandidateOTP WHERE RegNo = @RegNo",
                RegNoParam(regNo));

            // No row yet: this is the first OTP for the candidate.
            if (seconds == null || seconds == DBNull.Value)
                return int.MaxValue;

            return Convert.ToInt32(seconds);
        }

        private bool SaveOTP(string regNo, string otp)
        {
            byte[] salt = GenerateOTPSalt();
            byte[] hash = HashOTP(otp, salt);

            string connectionString =
                ConfigurationManager.ConnectionStrings["Objection_ConnectionString"].ConnectionString;

            using (SqlConnection con = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand("dbo.UpdateCandidateOTP", con))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@Regno", SqlDbType.VarChar, 10).Value = regNo;
                cmd.Parameters.Add("@OTPHash", SqlDbType.VarBinary, 32).Value = hash;
                cmd.Parameters.Add("@OTPSalt", SqlDbType.VarBinary, 16).Value = salt;

                SqlParameter returnValue = cmd.Parameters.Add("@ReturnVal", SqlDbType.Int);
                returnValue.Direction = ParameterDirection.ReturnValue;

                con.Open();
                cmd.ExecuteNonQuery();

                return Convert.ToInt32(returnValue.Value) == 1;
            }
        }

        private void InvalidateOTP(string regNo)
        {
            sh.ExecuteNonQuery(
                "UPDATE dbo.CandidateOTP SET IsUsed = 1 WHERE RegNo = @RegNo",
                RegNoParam(regNo));
        }

        private static SqlParameter RegNoParam(string regNo)
        {
            return new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = regNo };
        }

        // ---------------------------------------------------------------
        // OTP generation and hashing
        // ---------------------------------------------------------------

        private string GenerateOTP()
        {
            byte[] bytes = new byte[4];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            uint number = BitConverter.ToUInt32(bytes, 0);

            return (number % 1000000).ToString("D6");
        }

        private byte[] GenerateOTPSalt()
        {
            byte[] salt = new byte[16];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            return salt;
        }

        private byte[] HashOTP(string otp, byte[] salt)
        {
            byte[] pepperBytes = Encoding.UTF8.GetBytes(
                ConfigurationManager.AppSettings["OtpPepper"]);
            byte[] otpBytes = Encoding.UTF8.GetBytes(otp);

            byte[] combined = new byte[salt.Length + otpBytes.Length + pepperBytes.Length];

            System.Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
            System.Buffer.BlockCopy(otpBytes, 0, combined, salt.Length, otpBytes.Length);
            System.Buffer.BlockCopy(pepperBytes, 0, combined, salt.Length + otpBytes.Length, pepperBytes.Length);

            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(combined);
            }
        }

        // ---------------------------------------------------------------
        // SMS gateway
        // ---------------------------------------------------------------

        private bool SendSMS(string mobileNumber, string otp)
        {
            string username = ConfigurationManager.AppSettings["SMSUsername"];
            string password = ConfigurationManager.AppSettings["SMSPassword"];
            string senderid = ConfigurationManager.AppSettings["SMSSenderId"];
            string secureKey = ConfigurationManager.AppSettings["SMSSecureKey"];
            string templateid = ConfigurationManager.AppSettings["SMSTemplateId"];

            // Must match the registered DLT template exactly.
            string message = "Your KPSC Key Answer Objection portal login OTP is " + otp + ".It is valid for 5 minutes.Do not share it.-KPSC";

            try
            {
                SendSingleSMS(username, password, senderid, mobileNumber, message, secureKey, templateid);

                // The gateway's success response format has not been confirmed,
                // so any reply without an HTTP error is treated as accepted.
                return true;
            }
            catch (Exception ex)
            {
                // Never log the OTP or credentials.
                AppLog.Error("KPSC SMS Gateway error: " + ex);

                return false;
            }
        }

        private string SendSingleSMS(string username, string password, string senderid, string mobileNo, string message, string secureKey, string templateid)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(ConfigurationManager.AppSettings["SMSUrl"]);

            request.Method = "POST";
            request.ProtocolVersion = HttpVersion.Version10;
            request.KeepAlive = false;
            request.Timeout = SmsTimeoutMs;
            request.ReadWriteTimeout = SmsTimeoutMs;
            request.UserAgent = "Mozilla/4.0 (compatible; MSIE 5.0; Windows 98; DigExt)";
            request.ContentType = "application/x-www-form-urlencoded";

            string query =
                "username=" + HttpUtility.UrlEncode(username.Trim()) +
                "&password=" + HttpUtility.UrlEncode(Sha1Hex(password)) +
                "&smsservicetype=" + HttpUtility.UrlEncode("singlemsg") +
                "&content=" + HttpUtility.UrlEncode(message.Trim()) +
                "&mobileno=" + HttpUtility.UrlEncode(mobileNo) +
                "&senderid=" + HttpUtility.UrlEncode(senderid.Trim()) +
                "&key=" + HttpUtility.UrlEncode(MessageKey(username.Trim(), senderid.Trim(), message.Trim(), secureKey.Trim())) +
                "&templateid=" + HttpUtility.UrlEncode(templateid.Trim());

            byte[] body = Encoding.ASCII.GetBytes(query);
            request.ContentLength = body.Length;

            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            using (WebResponse response = request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>SHA-1 of the gateway password, as the gateway requires.</summary>
        private static string Sha1Hex(string value)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                return ToHex(sha1.ComputeHash(Encoding.UTF8.GetBytes(value)));
            }
        }

        /// <summary>SHA-512 of user + sender + message + secure key, as the gateway requires.</summary>
        private static string MessageKey(string username, string senderId, string message, string secureKey)
        {
            using (SHA512 sha512 = SHA512.Create())
            {
                return ToHex(sha512.ComputeHash(
                    Encoding.UTF8.GetBytes(username + senderId + message + secureKey)));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        // ---------------------------------------------------------------
        // Messages
        // ---------------------------------------------------------------

        private void ShowError(string message)
        {
            lblMessage.CssClass = "text-danger mt-3 d-block login-message";
            lblMessage.Text = message;
        }

        private void ShowSuccess(string message)
        {
            lblMessage.CssClass = "text-success mt-3 d-block login-message";
            lblMessage.Text = message;
        }
    }
}
