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

            base.OnInit(e);
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

            base.OnInit(e);
        }
    }
}
