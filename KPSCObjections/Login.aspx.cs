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
        private const string CaptchaKey = "CaptchaCode";

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
            RequestOtp();
        }

        private void RequestOtp()
        {
            string regNo = txtRegisterNumber.Text.Trim();
            string examCode = ddlExamination.SelectedIndex > 0
                ? ddlExamination.SelectedValue.Trim()
                : "";

            // The captcha is single use: whatever happens next, a new one is needed.
            bool captchaOk = ConsumeCaptcha();

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

            if (!captchaOk)
            {
                ShowError("Please enter the correct captcha.");
                return;
            }

            string mobile = GetRegisteredMobile(regNo, examCode);

            if (mobile == null)
            {
                ShowError("Registration Number and Examination do not match our records.");
                return;
            }

            if (!MobilePattern.IsMatch(mobile))
            {
                ShowError("No valid mobile number is registered for this candidate. Please contact KPSC.");
                return;
            }

            if (SecondsSinceLastSend(regNo) < ResendWaitSeconds)
            {
                ShowError("Please wait " + ResendWaitSeconds + " seconds before requesting another OTP.");
                return;
            }

            string otp = GenerateOTP();

            if (!SaveOTP(regNo, otp))
            {
                ShowError("Unable to generate OTP. Please try again later.");
                return;
            }

            if (!SendSMS(mobile, otp))
            {
                InvalidateOTP(regNo);
                ShowError("Unable to send OTP. Please try again later.");
                return;
            }

            Session[OtpRegNoKey] = regNo;
            Session[OtpExamKey] = examCode;

            txtOTP.Text = "";

            ShowSuccess(
                "OTP has been sent to your registered mobile number ending with " +
                mobile.Substring(mobile.Length - 4) + ".");
        }

        // ---------------------------------------------------------------
        // Step 2: verify the OTP and sign in
        // ---------------------------------------------------------------

        protected void btnLogin_Click(object sender, EventArgs e)
        {
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

            switch (CheckOtp(regNo, enteredOTP))
            {
                case OtpCheck.Ok:
                    break;

                case OtpCheck.Wrong:
                    ShowError("Invalid OTP. Please try again.");
                    return;

                case OtpCheck.Expired:
                    ShowError("OTP expired. Please request a new OTP.");
                    return;

                case OtpCheck.TooManyAttempts:
                    ShowError("Maximum OTP attempts exceeded. Please request a new OTP.");
                    return;

                case OtpCheck.Used:
                    ShowError("This OTP has already been used. Please request a new OTP.");
                    return;

                default:
                    ShowError("OTP not found. Please request a new OTP.");
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

        /// <summary>Checks the entered captcha and always retires it.</summary>
        private bool ConsumeCaptcha()
        {
            string entered = txtCaptcha.Text.Trim();
            string expected = Session[CaptchaKey] as string;

            Session.Remove(CaptchaKey);
            RefreshCaptcha();

            return !string.IsNullOrEmpty(expected)
                && string.Equals(entered, expected, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshCaptcha()
        {
            txtCaptcha.Text = "";
            imgCaptcha.ImageUrl = "~/Captcha.aspx?refresh=" + Guid.NewGuid().ToString("N");
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
                System.Diagnostics.Trace.TraceError("KPSC SMS Gateway error: " + ex.Message);

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
            lblMessage.CssClass = "text-danger mt-3 d-block";
            lblMessage.Text = message;
        }

        private void ShowSuccess(string message)
        {
            lblMessage.CssClass = "text-success mt-3 d-block";
            lblMessage.Text = message;
        }
    }
}
