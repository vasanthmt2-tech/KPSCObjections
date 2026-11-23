using System;
using System.Configuration;
using System.Web;

namespace KPSCMarksObjection.Security
{
    /// <summary>
    /// Headers added to every HTML page (static headers such as
    /// X-Frame-Options and Referrer-Policy are in Web.config):
    ///
    ///  - Content-Security-Policy: scripts, styles and fonts only from this
    ///    site and the Bootstrap CDN; forms may post only to this site; no
    ///    plugins; the page cannot be framed by other sites.
    ///    'unsafe-inline' is needed by ASP.NET Web Forms (__doPostBack and
    ///    ScriptManager write inline script).
    ///  - Cache-Control: no-store, so pages (which may show a candidate's
    ///    details) are not kept by browsers or proxies.
    /// </summary>
    public static class SecurityHeaders
    {
        private const string Cdn = "https://cdn.jsdelivr.net";

        private static string policy;

        /// <summary>
        /// Call from Application_PostRequestHandlerExecute: the page has run, so its
        /// content type is known, and ASP.NET has not yet written its cache headers
        /// (setting them later is overwritten).
        /// </summary>
        public static void ApplyCachePolicy(HttpContext context)
        {
            if (IsHtml(context.Response) && !context.Response.HeadersWritten)
            {
                context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
                context.Response.Cache.SetNoStore();
                context.Response.Cache.SetExpires(DateTime.UtcNow.AddYears(-1));
            }
        }

        /// <summary>Call from Application_PreSendRequestHeaders (also covers error pages).</summary>
        public static void Apply(HttpContext context)
        {
            if (IsHtml(context.Response))
                context.Response.Headers.Set("Content-Security-Policy", Policy());
        }

        private static bool IsHtml(HttpResponse response)
        {
            return (response.ContentType ?? "").StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
        }

        private static string Policy()
        {
            if (policy != null)
                return policy;

            policy =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' " + Cdn + "; " +
                "style-src 'self' 'unsafe-inline' " + Cdn + "; " +
                "font-src 'self' data: " + Cdn + "; " +
                "img-src 'self' data:; " +
                "connect-src 'self'; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "frame-ancestors 'self'; " +
                "form-action 'self'";

            return policy;
        }
    }
}
