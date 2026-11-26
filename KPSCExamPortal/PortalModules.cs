using System;
using System.Configuration;
using System.Web;

namespace KPSCExamPortal
{
    /// <summary>
    /// Switches for whole portal modules, set in Web.config appSettings.
    ///   Modules.KeyAnswerObjections = false : the answer key objection module is
    ///   hidden. Its menu items, home page card and admin links are not shown,
    ///   and every page under ~/Objections/ and ~/Admin/Objections/ answers 404
    ///   (except the Objection Window page, which also sets the marks window).
    /// A missing setting means the module is on. Set it back to true to bring
    /// the module back; nothing is deleted while it is off.
    /// </summary>
    public static class PortalModules
    {
        public static bool KeyAnswerObjections
        {
            get { return Flag("Modules.KeyAnswerObjections"); }
        }

        /// <summary>
        /// Called at the start of every request: a page of a switched-off module
        /// is answered as if it did not exist.
        /// </summary>
        public static void RejectIfSwitchedOff(HttpContext context)
        {
            if (KeyAnswerObjections)
                return;

            string path = context.Request.AppRelativeCurrentExecutionFilePath ?? "";

            bool keyAnswerPage =
                path.StartsWith("~/Objections/", StringComparison.OrdinalIgnoreCase) ||
                (path.StartsWith("~/Admin/Objections/", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("~/Admin/Objections/ObjectionWindow", StringComparison.OrdinalIgnoreCase));

            if (keyAnswerPage)
                throw new HttpException(404, "Not found");
        }

        private static bool Flag(string key)
        {
            string value = ConfigurationManager.AppSettings[key];

            return string.IsNullOrWhiteSpace(value) ||
                   !string.Equals(value.Trim(), "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}
