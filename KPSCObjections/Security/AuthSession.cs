using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace KPSCObjections.Security
{
    /// <summary>
    /// Sign-in state for candidates and admins.
    ///
    /// On sign-in a random token is stored in the session and in a separate
    /// HttpOnly cookie. A request counts as signed in only when both match, so
    /// a session id that was known before login (session fixation) is useless
    /// on its own.
    /// </summary>
    public static class AuthSession
    {
        private const string TokenKey = "AuthToken";
        private const string TokenCookie = "KPSCObjAuth";
        private const string SessionCookie = "ASP.NET_SessionId";

        public const string CandidateKey = "LoginValid";
        public const string AdminKey = "AdminUser";

        public static void SignInCandidate(HttpContext context, ObjectionSession.LoginDetails details)
        {
            context.Session.Clear();

            context.Session[CandidateKey] = details;
            context.Session["CandidateName"] = details.Name;
            context.Session["RegNoDesc"] = "Reg. No." + details.RegNo;
            context.Session["RegNo"] = details.RegNo;

            IssueToken(context);
        }

        public static void SignInAdmin(HttpContext context, string userName)
        {
            context.Session.Clear();
            context.Session[AdminKey] = userName;

            IssueToken(context);
        }

        public static bool IsCandidate(HttpContext context)
        {
            return context.Session != null
                && context.Session[CandidateKey] != null
                && TokenMatches(context);
        }

        public static bool IsAdmin(HttpContext context)
        {
            return context.Session != null
                && context.Session[AdminKey] != null
                && TokenMatches(context);
        }

        public static void SignOut(HttpContext context)
        {
            if (context.Session != null)
            {
                context.Session.Clear();
                context.Session.Abandon();
            }

            ExpireCookie(context, TokenCookie);
            ExpireCookie(context, SessionCookie);
        }

        private static void IssueToken(HttpContext context)
        {
            byte[] bytes = new byte[32];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            string token = Convert.ToBase64String(bytes);

            context.Session[TokenKey] = token;

            context.Response.Cookies.Set(new HttpCookie(TokenCookie, token)
            {
                HttpOnly = true,
                Secure = context.Request.IsSecureConnection,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        }

        private static bool TokenMatches(HttpContext context)
        {
            string expected = context.Session[TokenKey] as string;
            HttpCookie cookie = context.Request.Cookies[TokenCookie];

            if (string.IsNullOrEmpty(expected) || cookie == null || string.IsNullOrEmpty(cookie.Value))
                return false;

            return PasswordHasher.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(cookie.Value));
        }

        private static void ExpireCookie(HttpContext context, string name)
        {
            context.Response.Cookies.Set(new HttpCookie(name, "")
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true,
                Path = "/"
            });
        }
    }
}
