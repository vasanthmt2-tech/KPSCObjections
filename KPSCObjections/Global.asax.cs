using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
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
    }
}
