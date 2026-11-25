using System;
using System.Web;
using System.Web.UI;
using KPSCExamPortal.Security;

namespace KPSCExamPortal
{
    public partial class AdminMaster : SecureMasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            bool isLoggedIn = AuthSession.IsAdmin(Context);

            pnlNavigation.Visible = isLoggedIn;

            if (isLoggedIn)
            {
                lblAdminName.Text = HttpUtility.HtmlEncode(Convert.ToString(Session[AuthSession.AdminKey]));
            }

            // Admin pages enforce sign-in themselves by inheriting AdminPage.
            // This is a backstop for a page that uses this master without it.
            if (!isLoggedIn && !(Page is KPSCExamPortal.Admin.AdminLogin))
            {
                Response.Redirect("~/Admin/Login.aspx", true);
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            if (AuthSession.IsAdmin(Context))
                AdminAudit.Record(Context, AdminAudit.SignOut, "");

            AuthSession.SignOut(Context);

            Response.Redirect("~/Admin/Login.aspx", true);
        }
    }
}
