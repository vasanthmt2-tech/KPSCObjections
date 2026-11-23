using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace KPSCMarksObjection
{
    public partial class SiteMaster : KPSCMarksObjection.Security.SecureMasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // The menu, the candidate's name and Logout appear only after sign-in.
            // Set on every request (postbacks too), and before button events run,
            // so Logout still works for a signed-in candidate.
            pnlNavigation.Visible = KPSCMarksObjection.Security.AuthSession.IsCandidate(Context);

            if (!IsPostBack)
            {
                lblCandidateName.Text =
                    Convert.ToString(Session["CandidateName"]);

                lblRegisterNumber.Text =
                    Convert.ToString(Session["RegNoDesc"]);
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            KPSCMarksObjection.Security.AuthSession.SignOut(Context);

            Response.Redirect("~/Login.aspx", true);
        }
    }
}