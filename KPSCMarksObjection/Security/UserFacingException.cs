using System;

namespace KPSCMarksObjection.Security
{
    /// <summary>
    /// An error whose message was written for the user (a validation or
    /// business rule failure) and is safe to show on the page. Every other
    /// exception is logged and the user sees only a generic message, so
    /// database, file system or framework details never reach the browser.
    /// </summary>
    public class UserFacingException : Exception
    {
        public UserFacingException(string message) : base(message)
        {
        }
    }

    public static class ErrorReport
    {
        /// <summary>
        /// Logs the full exception under a short reference and returns a
        /// generic message carrying that reference, for the page to show.
        /// A <see cref="UserFacingException"/> is returned as is, unlogged.
        /// </summary>
        public static string Handle(Exception ex, string context, string genericMessage)
        {
            if (ex is UserFacingException)
                return ex.Message;

            string reference = NewReference();

            AppLog.Error("[" + reference + "] " + context + Environment.NewLine + ex);

            return genericMessage + " (Reference: " + reference + ")";
        }

        /// <summary>Short random reference linking a message the user sees to its log entry.</summary>
        public static string NewReference()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }
    }
}
