using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.UI;
using KPSCMarksObjection.Security;

namespace KPSCMarksObjection
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Trace.TraceError(...) anywhere in the app ends up in App_Data\logs.
            Trace.Listeners.Add(new AppLogTraceListener());

            // TEMPORARY test switches (captcha / OTP off) are announced in the log.
            KPSCMarksObjection.Security.TestMode.LogIfOn();

            // PDFsharp 6 (platform-independent build) must be told to use the fonts
            // installed in Windows (Arial, for the header stamped on supporting PDFs).
            // It may be set only once and before PDFsharp reads it, so it is done
            // here, before any PDF is processed.
            PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

            // Key pair for encrypting form submissions in the browser.
            EncryptedForms.Initialize();
        }

        void Application_BeginRequest(object sender, EventArgs e)
        {
            if (HttpsPolicy.OnBeginRequest(Context))
                return;

            // Form posts arrive encrypted; decrypt them before ASP.NET reads the
            // body, and refuse plain posts.
            if (EncryptedForms.OnBeginRequest(Context))
                return;

            KeepPostsToAspxPages();

            // Before session state is acquired, so a flood creates no sessions.
            Captcha.RejectIfFlooding(Context);
        }

        /// <summary>
        /// FriendlyUrls answers "X.aspx" with a 301 redirect to "X". A browser
        /// follows a 301 with a GET and drops the posted form, so a POST to
        /// X.aspx would arrive empty. Serve such a POST at the extensionless address instead
        /// of redirecting it. GET requests are still redirected as before.
        /// </summary>
        private void KeepPostsToAspxPages()
        {
            if (Request.HttpMethod != "POST")
                return;

            string path = Request.AppRelativeCurrentExecutionFilePath ?? "";

            if (!path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
                return;

            string extensionless = path.Substring(0, path.Length - ".aspx".Length);

            // Only for real pages, so nothing else becomes reachable this way.
            if (!System.IO.File.Exists(Server.MapPath(path)))
                return;

            Context.RewritePath(extensionless, Request.PathInfo, Request.Url.Query.TrimStart('?'));
        }

        /// <summary>Pages are never stored by browsers or proxies (see SecurityHeaders).</summary>
        void Application_PostRequestHandlerExecute(object sender, EventArgs e)
        {
            SecurityHeaders.ApplyCachePolicy(Context);
        }

        /// <summary>Finishes a sign-in in the new session (see AuthSession).</summary>
        void Application_PostAcquireRequestState(object sender, EventArgs e)
        {
            AuthSession.CompletePendingSignIn(Context);
        }

        /// <summary>
        /// Drops the Server header (e.g. "Microsoft-IIS/10.0") from every
        /// response, including static files and error pages, so the server
        /// software and version are not advertised.
        /// </summary>
        void Application_PreSendRequestHeaders(object sender, EventArgs e)
        {
            HttpContext context = HttpContext.Current;

            if (context != null)
            {
                context.Response.Headers.Remove("Server");
                SecurityHeaders.Apply(context);
            }
        }

        void Application_EndRequest(object sender, EventArgs e)
        {
            HttpsPolicy.OnEndRequest(Context);
        }

        /// <summary>
        /// Handles every unhandled error. Bad requests are answered here;
        /// real faults are logged and left in place so that customErrors in
        /// Web.config shows the generic error page.
        /// </summary>
        void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();

            if (ex == null)
                return;

            if (ex is HttpUnhandledException && ex.InnerException != null)
                ex = ex.InnerException;

            // Missing pages and the like are not application faults.
            HttpException http = ex as HttpException;

            if (http != null && http.GetHttpCode() == 404)
                return;

            // Bad requests are the client's doing, not application faults. They
            // get a short answer and one log line (no stack trace, so a flood of
            // them cannot fill the log):
            //  - a post whose ViewState, event validation or anti-forgery token
            //    was altered in the browser (tampering);
            //  - input that looks like script or markup (request validation);
            //  - an upload over the size limit.
            Exception last = Server.GetLastError();
            string tampering = TamperingReason(last);

            if (tampering != null)
            {
                Reject(400, "Tampered request rejected (" + tampering + ")",
                       "The request was rejected. Please reload the page and try again.");
                return;
            }

            if (Find<HttpRequestValidationException>(last) != null)
            {
                Reject(400, "Request with script or markup in its input rejected",
                       "The request was rejected because it contains characters that are not allowed " +
                       "(such as < or >). Please remove them and try again.");
                return;
            }

            if (IsPostTooLarge(last))
            {
                Reject(413, "Upload over the size limit rejected",
                       "The file is too large. Please upload a smaller file.");
                return;
            }

            if (http != null && http.GetHttpCode() == 400)
            {
                Reject(400, "Malformed request rejected", "The request was rejected.");
                return;
            }

            // A real fault: log it in full under a reference that the error page
            // shows, so a user's report can be matched to the log entry. Nothing
            // about the error itself is shown (customErrors mode="On").
            string reference = ErrorReport.NewReference();

            Context.Items[ErrorReferenceKey] = reference;

            AppLog.Error("[" + reference + "] " + RequestLine() + Environment.NewLine + ex);
        }

        /// <summary>Read by Error.aspx to show the reference of the logged error.</summary>
        public const string ErrorReferenceKey = "ErrorReference";

        private void Reject(int status, string logMessage, string userMessage)
        {
            AppLog.Write("WARN", logMessage + " from " + RequestThrottle.ClientIp(Context) + " " + RequestLine());

            Server.ClearError();
            Response.Clear();
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            Response.ContentType = "text/plain";
            Response.Write(userMessage);
            Context.ApplicationInstance.CompleteRequest();
        }

        /// <summary>Method and path only: query strings and form values are never logged.</summary>
        private string RequestLine()
        {
            try
            {
                return Request.HttpMethod + " " + Request.Path;
            }
            catch (HttpException)
            {
                // Request is not available during application start.
                return "(no request)";
            }
        }

        /// <summary>The request body was over maxRequestLength (checked through the whole chain:
        /// the page wraps it in HttpUnhandledException, which is itself an HttpException).</summary>
        private static bool IsPostTooLarge(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                HttpException http = e as HttpException;

                if (http != null &&
                    (http.WebEventCode == System.Web.Management.WebEventCodes.RuntimeErrorPostTooLarge ||
                     http.Message == "Maximum request length exceeded."))
                    return true;
            }

            return false;
        }

        private static T Find<T>(Exception ex) where T : Exception
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is T)
                    return (T)e;
            }

            return null;
        }

        /// <summary>What was tampered with, or null when the error is something else.</summary>
        private static string TamperingReason(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is EncryptedRequestException)
                    return "encrypted form: " + e.Message;

                if (e is ViewStateException)
                    return "ViewState";

                if (e is ArgumentException && e.Message.StartsWith("Invalid postback or callback argument", StringComparison.Ordinal))
                    return "event validation";

                if (e is InvalidOperationException && e.Message == SecureMasterPage.AntiXsrfFailedMessage)
                    return "anti-forgery token";

                HttpException http = e as HttpException;

                if (http != null && http.WebEventCode == System.Web.Management.WebEventCodes.RuntimeErrorViewStateFailure)
                    return "ViewState";
            }

            return null;
        }
    }
}
