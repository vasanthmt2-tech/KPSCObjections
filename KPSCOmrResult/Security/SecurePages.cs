using System;
using System.Web.UI;

namespace KPSCOmrResult.Security
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
                // Read before SignOut: a sign-in cookie without a valid session
                // means the session ended, and the login page says so.
                bool expired = AuthSession.HadSignIn(Context);

                // A signed-in admin who opens a candidate page keeps the admin
                // sign-in; otherwise a link to a candidate page (from any site)
                // could sign an admin out. An invalid session was already
                // cleared by the check above.
                if (!AuthSession.IsAdmin(Context))
                    AuthSession.SignOut(Context);

                Response.Redirect(expired ? "~/Login.aspx?expired=1" : "~/Login.aspx", true);
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
                bool expired = AuthSession.HadSignIn(Context);

                // A signed-in candidate who opens an admin page keeps the
                // candidate sign-in; otherwise a link to an admin page (from any
                // site) could sign a candidate out. An invalid session was
                // already cleared by the check above.
                if (!AuthSession.IsCandidate(Context))
                    AuthSession.SignOut(Context);

                Response.Redirect(expired ? "~/AdminLogin.aspx?expired=1" : "~/AdminLogin.aspx", true);
            }

            CandidatePage.NoStore(Response);

            base.OnInit(e);
        }
    }
}
