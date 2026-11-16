using System;
using System.Web;
using System.Web.UI;
using KPSCObjections.Security;

namespace KPSCObjections
{
    public partial class AdminLogin : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && AuthSession.IsAdmin(Context))
            {
                Response.Redirect("~/Dashboard.aspx", true);
            }

            if (!IsPostBack)
                RefreshCaptcha();
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            string userName = txtEmpId.Text.Trim();
            string password = txtEmpPwd.Text;

            // Per-IP limit on admin login attempts. The per-account lockout does not
            // stop one wrong password tried against many accounts; this does.
            // Checked before the captcha is used up.
            if (!RequestThrottle.AllowLoginAttempt(Context, "admin login", "AdminLoginPerIpPerMinute", 10))
            {
                Alert(RequestThrottle.TooManyAttemptsMessage);
                return;
            }

            // The captcha is single use: whatever happens next, a new one is shown.
            bool captchaOk = Captcha.Consume(CaptchaToken, txtCaptcha.Text);
            RefreshCaptcha();

            if (string.IsNullOrWhiteSpace(userName))
            {
                txtEmpId.Focus();
                Alert("Please enter the user name.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                txtEmpPwd.Focus();
                Alert("Please enter the Password.");
                return;
            }

            // Checked before the password, so automated guessing cannot make the
            // server run the (deliberately slow) password hash, and a wrong
            // captcha does not count towards the account lockout.
            if (!captchaOk)
            {
                txtCaptcha.Focus();
                Alert("Please enter the correct captcha.");
                return;
            }

            if (userName.Length > 50 || password.Length > 200)
            {
                Alert(FailedLoginMessage);
                return;
            }

            AdminAuth.Result result;

            try
            {
                result = AdminAuth.Verify(userName, password);
            }
            catch (Exception ex)
            {
                Alert(ErrorReport.Handle(ex, "Admin login failed",
                    "Login is temporarily unavailable. Please try again later."));
                return;
            }

            switch (result)
            {
                case AdminAuth.Result.Ok:
                    AdminAudit.Record(Context, userName, AdminAudit.SignIn, "");
                    AuthSession.SignInAdmin(Context, userName);
                    Response.Redirect("~/Dashboard.aspx", true);
                    break;

                default:
                    // Same message for a wrong password, an unknown user and a
                    // locked account: "locked" would confirm the user name exists.
                    // The lockout itself still applies (AdminAuth). The audit
                    // trail (admins only) does record which it was.
                    AdminAudit.Record(Context, userName, AdminAudit.SignInFailed,
                        result == AdminAuth.Result.LockedOut
                            ? "Account locked (too many wrong passwords)"
                            : "Wrong user name or password");
                    Alert(FailedLoginMessage);
                    break;
            }
        }

        private const string FailedLoginMessage =
            "Invalid username or password. After 5 failed attempts the account is locked for 15 minutes.";

        protected void btnRefreshCaptcha_Click1(object sender, EventArgs e)
        {
            RefreshCaptcha();
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

        private void Alert(string message)
        {
            ScriptManager.RegisterStartupScript(
                this, GetType(), "alert",
                "alert('" + HttpUtility.JavaScriptStringEncode(message) + "');", true);
        }
    }
}
