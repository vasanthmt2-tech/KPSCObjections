using System;
using System.Web;
using System.Web.UI;
using KPSCObjections.Security;

namespace KPSCObjections
{
    public partial class Admin : System.Web.UI.MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            bool isLoggedIn = AuthSession.IsAdmin(Context);

            pnlNavigation.Visible = isLoggedIn;

            if (isLoggedIn)
            {
                lblAdminName.Text = Convert.ToString(Session[AuthSession.AdminKey]);
            }

            // Admin pages enforce sign-in themselves by inheriting AdminPage.
            // This is a backstop for a page that uses this master without it.
            if (!isLoggedIn && !(Page is AdminLogin))
            {
                Response.Redirect("~/AdminLogin.aspx", true);
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            AuthSession.SignOut(Context);

            Response.Redirect("~/AdminLogin.aspx", true);
        }
    }
}
