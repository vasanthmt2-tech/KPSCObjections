using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace KPSCObjections
{
    public partial class SiteMaster : KPSCObjections.Security.SecureMasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // The menu, the candidate's name and Logout appear only after sign-in.
            // Set on every request (postbacks too), and before button events run,
            // so Logout still works for a signed-in candidate.
            pnlNavigation.Visible = KPSCObjections.Security.AuthSession.IsCandidate(Context);

            if (!IsPostBack)
            {
                lblCandidateName.Text =
                    HttpUtility.HtmlEncode(Convert.ToString(Session["CandidateName"]));

                lblRegisterNumber.Text =
                    HttpUtility.HtmlEncode(Convert.ToString(Session["RegNoDesc"]));
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            KPSCObjections.Security.AuthSession.SignOut(Context);

            Response.Redirect("~/Login.aspx", true);
        }
    }
}