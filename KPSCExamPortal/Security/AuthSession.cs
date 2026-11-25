using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace KPSCExamPortal.Security
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
        private const string TokenCookie = "KPSCExamAuth";
        /// <summary>
        /// The session cookie's name, as configured in sessionState cookieName
        /// (unique to this portal, so signing out does not end the session of
        /// another application on the same site).
        /// </summary>
        private static readonly string SessionCookie =
            ((System.Web.Configuration.SessionStateSection)
                System.Web.Configuration.WebConfigurationManager.GetSection("system.web/sessionState")).CookieName;

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

        // ---------------------------------------------------------------
        // Sign-in with a new session ID
        //
        // ASP.NET cannot change a session's ID during a request, so a sign-in
        // takes two steps:
        //  1. SignIn*: the pre-login session is ended and its cookie removed,
        //     and the sign-in is parked on the server under a random one-time
        //     ticket (cookie, 2 minutes, bound to the browser).
        //  2. On the next request (the redirect that follows every sign-in)
        //     ASP.NET issues a brand-new session ID, and CompletePendingSignIn
        //     moves the sign-in into that new session.
        // So the session ID before login never becomes a signed-in session.
        // ---------------------------------------------------------------

        private const string TicketCookie = "KPSCExam_SignIn";
        private const int TicketSeconds = 120;

        private sealed class PendingSignIn
        {
            public ObjectionSession.LoginDetails? Candidate;
            public string Admin;
            public string Agent;
        }

        /// <summary>Signs a candidate in. The caller must redirect afterwards.</summary>
        public static void SignInCandidate(HttpContext context, ObjectionSession.LoginDetails details)
        {
            BeginSignIn(context, new PendingSignIn { Candidate = details });
        }

        /// <summary>Signs an admin in. The caller must redirect afterwards.</summary>
        public static void SignInAdmin(HttpContext context, string userName)
        {
            BeginSignIn(context, new PendingSignIn { Admin = userName });
        }

        private static void BeginSignIn(HttpContext context, PendingSignIn pending)
        {
            pending.Agent = AgentHash(context);

            string ticket = NewRandomToken();

            HttpRuntime.Cache.Insert(
                "signin:" + ticket, pending, null,
                DateTime.UtcNow.AddSeconds(TicketSeconds),
                System.Web.Caching.Cache.NoSlidingExpiration);

            // End the pre-login session and drop its ID and any earlier sign-in's
            // token from the browser.
            context.Session.Clear();
            context.Session.Abandon();
            ExpireCookie(context, SessionCookie);
            ExpireCookie(context, TokenCookie);

            context.Response.Cookies.Set(new HttpCookie(TicketCookie, ticket)
            {
                HttpOnly = true,
                Secure = HttpsPolicy.CookiesMustBeSecure(context),
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = DateTime.UtcNow.AddSeconds(TicketSeconds)
            });
        }

        /// <summary>
        /// Call from Application_PostAcquireRequestState. Finishes a sign-in
        /// started on the previous request, in the new session.
        /// </summary>
        public static void CompletePendingSignIn(HttpContext context)
        {
            HttpCookie cookie = context.Request.Cookies[TicketCookie];

            if (cookie == null || string.IsNullOrEmpty(cookie.Value) || context.Session == null)
                return;

            // Only the page the browser was redirected to may use the ticket: an
            // image or handler request (the captcha, a document) that happens to
            // run at the same moment must not take it, or the page would load
            // without the sign-in.
            if (!(context.Handler is System.Web.UI.Page) || context.Handler is Captcha)
                return;

            // Single use: whatever happens, the ticket is gone after this.
            PendingSignIn pending = HttpRuntime.Cache.Remove("signin:" + cookie.Value) as PendingSignIn;
            ExpireCookie(context, TicketCookie);

            if (pending == null || !SameText(pending.Agent, AgentHash(context)))
                return;

            context.Session.Clear();

            if (pending.Candidate.HasValue)
            {
                ObjectionSession.LoginDetails details = pending.Candidate.Value;

                context.Session[CandidateKey] = details;
                context.Session["CandidateName"] = details.Name;
                context.Session["RegNoDesc"] = "Reg. No." + details.RegNo;
                context.Session["RegNo"] = details.RegNo;

                IssueToken(context, CandidateUserKey(details));
            }
            else
            {
                context.Session[AdminKey] = pending.Admin;

                IssueToken(context, AdminUserKey(pending.Admin));
            }
        }

        public static bool IsCandidate(HttpContext context)
        {
            if (context.Session == null)
                return false;

            object details = context.Session[CandidateKey];

            if (details == null)
                return false;

            return IsValid(context, CandidateUserKey((ObjectionSession.LoginDetails)details));
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

        /// <summary>
        /// True when the browser still sends a sign-in cookie: this visitor was
        /// signed in and the session has since ended (idle time-out, maximum
        /// lifetime, or a newer sign-in elsewhere). Logout removes the cookie,
        /// so it is false after a deliberate sign-out.
        /// </summary>
        public static bool HadSignIn(HttpContext context)
        {
            HttpCookie cookie = context.Request.Cookies[TokenCookie];

            return cookie != null && !string.IsNullOrEmpty(cookie.Value);
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

        /// <summary>
        /// One sign-in per candidate per candidate list: the same registration
        /// number can be in the key answer list (OTP) and in the marks list (date
        /// of birth), and those are different people's sign-ins.
        /// </summary>
        private static string CandidateUserKey(ObjectionSession.LoginDetails details)
        {
            return "c:" + (details.Source ?? "") + ":" + (details.RegNo ?? "").Trim();
        }

        /// <summary>Which candidate list the signed-in candidate came from (ObjectionSession.KeySource), or null.</summary>
        public static string CandidateSource(HttpContext context)
        {
            object details = context.Session == null ? null : context.Session[CandidateKey];

            return details is ObjectionSession.LoginDetails ? ((ObjectionSession.LoginDetails)details).Source : null;
        }

        private static string AdminUserKey(string userName)
        {
            return "a:" + (userName ?? "").Trim();
        }

        private static string CurrentUserKey(HttpContext context)
        {
            object details = context.Session[CandidateKey];

            if (details != null)
                return CandidateUserKey((ObjectionSession.LoginDetails)details);

            string admin = context.Session[AdminKey] as string;

            return string.IsNullOrEmpty(admin) ? null : AdminUserKey(admin);
        }

        /// <summary>32 random bytes, URL-safe base64 (fit for a cookie or cache key).</summary>
        private static string NewRandomToken()
        {
            byte[] bytes = new byte[32];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static void IssueToken(HttpContext context, string userKey)
        {
            string token = NewRandomToken();

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

            // The rest of this request must check against the new token too, not
            // a token from an earlier sign-in that the browser sent in.
            context.Request.Cookies.Set(new HttpCookie(TokenCookie, token));
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
