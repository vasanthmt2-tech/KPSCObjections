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
using KPSCObjections.Security;

namespace KPSCObjections
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
        }

        void Application_BeginRequest(object sender, EventArgs e)
        {
            if (HttpsPolicy.OnBeginRequest(Context))
                return;

            // Before session state is acquired, so a flood creates no sessions.
            Captcha.RejectIfFlooding(Context);
        }

        void Application_EndRequest(object sender, EventArgs e)
        {
            HttpsPolicy.OnEndRequest(Context);
        }

        /// <summary>
        /// Logs every unhandled error. The error is left in place so that
        /// customErrors in Web.config decides what the visitor sees.
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

            // A post whose ViewState, event validation or anti-forgery token was
            // altered in the browser is an attack, not a fault: refuse it with
            // 400 and one log line (no stack trace, so it cannot flood the log).
            string tampering = TamperingReason(Server.GetLastError());

            if (tampering != null)
            {
                AppLog.Write("WARN", "Tampered request rejected (" + tampering + ") from " +
                             RequestThrottle.ClientIp(Context) + " " + Request.HttpMethod + " " + Request.Path);

                Server.ClearError();
                Response.Clear();
                Response.StatusCode = 400;
                Response.TrySkipIisCustomErrors = true;
                Response.ContentType = "text/plain";
                Response.Write("The request was rejected. Please reload the page and try again.");
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            string path = "";

            try
            {
                // Path only: query strings and form values are not logged.
                path = Request.HttpMethod + " " + Request.Path;
            }
            catch
            {
                // Request is not available during application start.
            }

            AppLog.Error(path + Environment.NewLine + ex);
        }

        /// <summary>What was tampered with, or null when the error is something else.</summary>
        private static string TamperingReason(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
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
