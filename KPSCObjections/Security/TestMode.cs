using System.Configuration;

namespace KPSCObjections.Security
{
    /// <summary>
    /// TEMPORARY switches for security testing (VAPT) of the login pages, set in
    /// Web.config appSettings:
    ///   TestMode.DisableCaptcha = true : no captcha on candidate and admin login.
    ///   TestMode.DisableOtp     = true : candidates log in with registration
    ///                                    number and examination only (no SMS).
    /// Both are false by default and must be false in production: with them on,
    /// anyone who knows a registration number can sign in as that candidate.
    /// When either is on, the login pages show a TEST MODE banner and the
    /// application log records a warning at start-up. All other checks (per-IP
    /// limits, admin password and lockout, session rules) stay in force.
    /// </summary>
    public static class TestMode
    {
        public static bool CaptchaDisabled
        {
            get { return Flag("TestMode.DisableCaptcha"); }
        }

        public static bool OtpDisabled
        {
            get { return Flag("TestMode.DisableOtp"); }
        }

        public static bool Any
        {
            get { return CaptchaDisabled || OtpDisabled; }
        }

        /// <summary>The banner text for the login pages, or "" when test mode is off.</summary>
        public static string Banner(bool candidatePage)
        {
            if (!Any)
                return "";

            string what = candidatePage
                ? (CaptchaDisabled && OtpDisabled ? "captcha and OTP are" : CaptchaDisabled ? "captcha is" : "OTP is")
                : (CaptchaDisabled ? "captcha is" : "");

            return what.Length == 0
                ? ""
                : "TEST MODE: " + what + " switched off for security testing. Not for live use.";
        }

        /// <summary>Writes a warning to the application log; called at start-up.</summary>
        public static void LogIfOn()
        {
            if (Any)
            {
                AppLog.Write("WARN", "TEST MODE is on: " +
                             (CaptchaDisabled ? "captcha disabled; " : "") +
                             (OtpDisabled ? "candidate OTP disabled (login with registration number and examination only); " : "") +
                             "set TestMode.DisableCaptcha and TestMode.DisableOtp to false in Web.config before going live.");
            }
        }

        private static bool Flag(string key)
        {
            bool on;
            return bool.TryParse(ConfigurationManager.AppSettings[key], out on) && on;
        }
    }
}
