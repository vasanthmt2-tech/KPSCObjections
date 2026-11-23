using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using KPSCOmrResult.DLL;

namespace KPSCOmrResult.Security
{
    /// <summary>
    /// Records admin actions in dbo.AdminAudit (Database\migrations\004_AdminAudit.sql),
    /// shown on the admin Audit Trail page. Each entry is also written to the
    /// application log, so the action is kept even if the database insert fails.
    ///
    /// Recording never throws: an audit failure must not undo or block the
    /// action it describes. Details must not contain passwords, OTPs or file
    /// contents.
    /// </summary>
    public static class AdminAudit
    {
        public const string SignIn = "Sign-in";
        public const string SignInFailed = "Sign-in failed";
        public const string SignOut = "Sign-out";
        public const string UploadCandidates = "Upload candidates";
        public const string UploadVersionCodes = "Upload version codes";
        public const string DeleteAllCandidates = "Delete all candidates";
        public const string UploadKeyAnswers = "Upload key answers";
        public const string DownloadKeyAnswers = "Download key answers";
        public const string DeleteKeyAnswers = "Delete key answers";
        public const string DownloadObjectionsExcel = "Download objections Excel";
        public const string DownloadObjectionsZip = "Download objection documents";
        public const string RecheckPayments = "Re-check pending payments";
        public const string ViewCandidateObjections = "View candidate objections";
        public const string ChangeObjectionWindow = "Change objection window";
        public const string DownloadAuditTrail = "Download audit trail";
        public const string UploadResults = "Upload OMR results";
        public const string DeleteResults = "Delete OMR results";

        private const int MaxDetails = 1000;

        /// <summary>Records an action of the signed-in admin.</summary>
        public static void Record(HttpContext context, string action, string details)
        {
            string admin = context != null && context.Session != null
                ? context.Session[AuthSession.AdminKey] as string
                : null;

            Record(context, admin, action, details);
        }

        /// <summary>Records an action for the given admin user name (used at sign-in).</summary>
        public static void Record(HttpContext context, string adminUser, string action, string details)
        {
            string user = Clip(string.IsNullOrWhiteSpace(adminUser) ? "(unknown)" : adminUser.Trim(), 50);
            string ip = context != null ? Clip(RequestThrottle.ClientIp(context), 45) : "";
            string text = Clip(details ?? "", MaxDetails);

            AppLog.Write("AUDIT", user + " | " + action + " | " + text + " | " + ip);

            try
            {
                new SqlHelper().ExecuteNonQuery(
                    "INSERT INTO dbo.AdminAudit (AdminUser, Action, Details, ClientIp) " +
                    "VALUES (@AdminUser, @Action, @Details, @ClientIp)",
                    new SqlParameter("@AdminUser", SqlDbType.VarChar, 50) { Value = user },
                    new SqlParameter("@Action", SqlDbType.VarChar, 50) { Value = Clip(action, 50) },
                    new SqlParameter("@Details", SqlDbType.NVarChar, MaxDetails) { Value = text },
                    new SqlParameter("@ClientIp", SqlDbType.VarChar, 45) { Value = ip });
            }
            catch (Exception ex)
            {
                AppLog.Error("Admin audit entry could not be saved (it is in the log above): " + ex.Message);
            }
        }

        private static string Clip(string value, int max)
        {
            value = value ?? "";
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }
}
