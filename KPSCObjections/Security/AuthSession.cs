using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace KPSCObjections.Security
{
    /// <summary>
    /// Sign-in state for candidates and admins, hardened against session
    /// fixation and session hijacking.
    ///
    ///  - On sign-in a random token is stored in the session and in a separate
    ///    HttpOnly cookie. A request counts as signed in only when both match,
    ///    so a session id known before login (fixation) is useless on its own.
    ///  - The sign-in is bound to the browser that signed in (User-Agent hash),
    ///    so cookies replayed from a different browser are rejected.
    ///  - A sign-in has an absolute lifetime (AuthMaxMinutes) on top of the
    ///    sliding idle timeout, so a stolen cookie cannot be kept alive forever.
    ///  - Only the latest sign-in of a user is valid. Signing in again (for
    ///    example after noticing something odd) ends every older session.
    ///  - Any mismatch ends the session and is logged.
    /// </summary>
    public static class AuthSession
    {
        private const string TokenKey = "AuthToken";
        private const string IssuedKey = "AuthIssuedUtc";
        private const string AgentKey = "AuthAgent";
        private const string TokenCookie = "KPSCObjAuth";
        private const string SessionCookie = "ASP.NET_SessionId";

        public const string CandidateKey = "LoginValid";
        public const string AdminKey = "AdminUser";

        private const int DefaultMaxMinutes = 120;

        /// <summary>
        /// Latest token per user ("c:RegNo" / "a:UserName"). Sessions are InProc,
        /// so this lives exactly as long as they do. If session state is ever
        /// moved out of process or onto several servers, move this to the database.
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> ActiveTokens =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void SignInCandidate(HttpContext context, ObjectionSession.LoginDetails details)
        {
            context.Session.Clear();

            context.Session[CandidateKey] = details;
            context.Session["CandidateName"] = details.Name;
            context.Session["RegNoDesc"] = "Reg. No." + details.RegNo;
            context.Session["RegNo"] = details.RegNo;

            IssueToken(context, CandidateUserKey(details.RegNo));
        }

        public static void SignInAdmin(HttpContext context, string userName)
        {
            context.Session.Clear();
            context.Session[AdminKey] = userName;

            IssueToken(context, AdminUserKey(userName));
        }

        public static bool IsCandidate(HttpContext context)
        {
            if (context.Session == null)
                return false;

            object details = context.Session[CandidateKey];

            if (details == null)
                return false;

            return IsValid(context, CandidateUserKey(((ObjectionSession.LoginDetails)details).RegNo));
        }

        public static bool IsAdmin(HttpContext context)
        {
            if (context.Session == null)
                return false;

            string userName = context.Session[AdminKey] as string;

            if (string.IsNullOrEmpty(userName))
                return false;

            return IsValid(context, AdminUserKey(userName));
        }

        public static void SignOut(HttpContext context)
        {
            if (context.Session != null)
            {
                string token = context.Session[TokenKey] as string;
                string userKey = CurrentUserKey(context);

                if (token != null && userKey != null)
                {
                    // Only forget the entry if it still belongs to this session,
                    // so logging out an old session does not end a newer one.
                    ((ICollection<KeyValuePair<string, string>>)ActiveTokens)
                        .Remove(new KeyValuePair<string, string>(userKey, token));
                }

                context.Session.Clear();
                context.Session.Abandon();
            }

            ExpireCookie(context, TokenCookie);
            ExpireCookie(context, SessionCookie);
        }

        // ---------------------------------------------------------------

        private static string CandidateUserKey(string regNo)
        {
            return "c:" + (regNo ?? "").Trim();
        }

        private static string AdminUserKey(string userName)
        {
            return "a:" + (userName ?? "").Trim();
        }

        private static string CurrentUserKey(HttpContext context)
        {
            object details = context.Session[CandidateKey];

            if (details != null)
                return CandidateUserKey(((ObjectionSession.LoginDetails)details).RegNo);

            string admin = context.Session[AdminKey] as string;

            return string.IsNullOrEmpty(admin) ? null : AdminUserKey(admin);
        }

        private static void IssueToken(HttpContext context, string userKey)
        {
            byte[] bytes = new byte[32];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            string token = Convert.ToBase64String(bytes);

            context.Session[TokenKey] = token;
            context.Session[IssuedKey] = DateTime.UtcNow;
            context.Session[AgentKey] = AgentHash(context);

            // Replaces any earlier sign-in of the same user.
            ActiveTokens[userKey] = token;

            context.Response.Cookies.Set(new HttpCookie(TokenCookie, token)
            {
                HttpOnly = true,
                Secure = HttpsPolicy.CookiesMustBeSecure(context),
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        }

        private static bool IsValid(HttpContext context, string userKey)
        {
            string expected = context.Session[TokenKey] as string;

            if (string.IsNullOrEmpty(expected))
                return false;

            string reason = null;
            HttpCookie cookie = context.Request.Cookies[TokenCookie];

            if (cookie == null || string.IsNullOrEmpty(cookie.Value) || !SameText(expected, cookie.Value))
            {
                reason = "auth cookie does not match session";
            }
            else if (!SameText(context.Session[AgentKey] as string ?? "", AgentHash(context)))
            {
                reason = "browser changed during session";
            }
            else if (!(context.Session[IssuedKey] is DateTime)
                  || DateTime.UtcNow - (DateTime)context.Session[IssuedKey] > TimeSpan.FromMinutes(MaxMinutes()))
            {
                reason = "absolute session lifetime exceeded";
            }
            else
            {
                string active;

                if (!ActiveTokens.TryGetValue(userKey, out active) || !SameText(expected, active))
                    reason = "superseded by a newer sign-in";
            }

            if (reason == null)
                return true;

            // A mismatched or expired session is never trusted again: clear it so
            // the caller's redirect to the login page starts from nothing.
            AppLog.Write("WARN", "Session rejected (" + reason + ") for " + userKey +
                         " from " + context.Request.UserHostAddress + " " + context.Request.Path);

            context.Session.Clear();

            return false;
        }

        private static int MaxMinutes()
        {
            int minutes;

            return int.TryParse(ConfigurationManager.AppSettings["AuthMaxMinutes"], out minutes) && minutes > 0
                ? minutes
                : DefaultMaxMinutes;
        }

        private static string AgentHash(HttpContext context)
        {
            string agent = context.Request.UserAgent ?? "";

            using (SHA256 sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(agent)));
            }
        }

        private static bool SameText(string a, string b)
        {
            return PasswordHasher.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
        }

        private static void ExpireCookie(HttpContext context, string name)
        {
            context.Response.Cookies.Set(new HttpCookie(name, "")
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true,
                Secure = HttpsPolicy.CookiesMustBeSecure(context),
                Path = "/"
            });
        }
    }
}
