using System;
using System.Web.UI;

namespace KPSCObjections.Security
{
    /// <summary>
    /// Base class for pages that need a signed-in candidate. The check runs in
    /// OnInit and ends the request, so no page code or postback handler runs
    /// for a visitor who is not signed in.
    /// </summary>
    public class CandidatePage : Page
    {
        protected override void OnInit(EventArgs e)
        {
            if (!AuthSession.IsCandidate(Context))
            {
                AuthSession.SignOut(Context);
                Response.Redirect("~/Login.aspx", true);
            }

            NoStore(Response);

            base.OnInit(e);
        }

        /// <summary>
        /// Signed-in pages are not kept by the browser, so Back after logout
        /// on a shared computer does not show another person's details.
        /// </summary>
        internal static void NoStore(System.Web.HttpResponse response)
        {
            response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            response.Cache.SetNoStore();
        }
    }

    /// <summary>Base class for pages that need a signed-in admin.</summary>
    public class AdminPage : Page
    {
        protected override void OnInit(EventArgs e)
        {
            if (!AuthSession.IsAdmin(Context))
            {
                Response.Redirect("~/AdminLogin.aspx", true);
            }

            CandidatePage.NoStore(Response);

            base.OnInit(e);
        }
    }
}
