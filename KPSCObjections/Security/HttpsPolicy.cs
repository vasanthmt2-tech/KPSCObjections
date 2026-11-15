using System;
using System.Configuration;
using System.Web;

namespace KPSCObjections.Security
{
    /// <summary>
    /// Keeps session and auth cookies off plain HTTP, where they could be
    /// sniffed and replayed (session hijacking).
    ///
    /// RequireHttps = true (appSettings, set it in production):
    ///   - plain HTTP requests are redirected to HTTPS;
    ///   - every cookie the site sets is marked Secure;
    ///   - browsers are told to use HTTPS only (HSTS).
    ///
    /// Even with RequireHttps = false, cookies set on an HTTPS request are
    /// always marked Secure.
    /// </summary>
    public static class HttpsPolicy
    {
        private const string HstsValue = "max-age=31536000";

        public static bool Required
        {
            get
            {
                bool value;
                return bool.TryParse(ConfigurationManager.AppSettings["RequireHttps"], out value) && value;
            }
        }

        public static bool CookiesMustBeSecure(HttpContext context)
        {
            return Required || context.Request.IsSecureConnection;
        }

        /// <summary>Call from Application_BeginRequest. Returns true when the request was redirected.</summary>
        public static bool OnBeginRequest(HttpContext context)
        {
            if (!Required)
                return false;

            if (!context.Request.IsSecureConnection)
            {
                // GET/HEAD can be redirected safely. Anything else over plain
                // HTTP is refused, so form data is never accepted unencrypted.
                if (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "HEAD")
                {
                    UriBuilder secure = new UriBuilder(context.Request.Url)
                    {
                        Scheme = Uri.UriSchemeHttps,
                        Port = -1
                    };

                    context.Response.RedirectPermanent(secure.Uri.ToString(), false);
                }
                else
                {
                    context.Response.StatusCode = 403;
                    context.Response.StatusDescription = "HTTPS Required";
                }

                context.ApplicationInstance.CompleteRequest();
                return true;
            }

            context.Response.AppendHeader("Strict-Transport-Security", HstsValue);
            return false;
        }

        /// <summary>
        /// Call from Application_EndRequest. Marks every outgoing cookie
        /// (including ASP.NET_SessionId and the anti-XSRF cookie) Secure and HttpOnly.
        /// </summary>
        public static void OnEndRequest(HttpContext context)
        {
            bool secure = CookiesMustBeSecure(context);

            HttpCookieCollection cookies = context.Response.Cookies;

            for (int i = 0; i < cookies.Count; i++)
            {
                HttpCookie cookie = cookies[i];

                cookie.HttpOnly = true;

                if (secure)
                    cookie.Secure = true;
            }
        }
    }
}
