using System.Configuration;

namespace KPSCMarksObjection.Security
{
    /// <summary>
    /// TEMPORARY switch for security testing (VAPT) of the login pages, set in
    /// Web.config appSettings:
    ///   TestMode.DisableCaptcha = true : no captcha on candidate and admin login.
    /// False by default and must be false in production: without the captcha,
    /// date-of-birth guessing is held back only by the per-IP limit and the
    /// per-candidate lockout. When it is on, the login pages show a TEST MODE
    /// banner and the application log records a warning at start-up. All
    /// other checks (per-IP limits, lockouts, admin password, session rules)
    /// stay in force.
    /// </summary>
    public static class TestMode
    {
        public static bool CaptchaDisabled
        {
            get { return Flag("TestMode.DisableCaptcha"); }
        }

        public static bool Any
        {
            get { return CaptchaDisabled; }
        }

        /// <summary>The banner text for the login pages, or "" when test mode is off.</summary>
        public static string Banner(bool candidatePage)
        {
            return CaptchaDisabled
                ? "TEST MODE: captcha is switched off for security testing. Not for live use."
                : "";
        }

        /// <summary>Writes a warning to the application log; called at start-up.</summary>
        public static void LogIfOn()
        {
            if (Any)
            {
                AppLog.Write("WARN", "TEST MODE is on: captcha disabled; " +
                             "set TestMode.DisableCaptcha to false in Web.config before going live.");
            }
        }

        private static bool Flag(string key)
        {
            bool on;
            return bool.TryParse(ConfigurationManager.AppSettings[key], out on) && on;
        }
    }
}
