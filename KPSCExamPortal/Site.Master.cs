using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace KPSCExamPortal
{
    public partial class SiteMaster : KPSCExamPortal.Security.SecureMasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // The menu, the candidate's name and Logout appear only after sign-in.
            // Set on every request (postbacks too), and before button events run,
            // so Logout still works for a signed-in candidate.
            pnlNavigation.Visible = KPSCExamPortal.Security.AuthSession.IsCandidate(Context);

            // The answer key objection module can be switched off (see PortalModules).
            lnkFileObjection.Visible = lnkMyObjections.Visible = PortalModules.KeyAnswerObjections;

            // The section's name under the heading.
            string path = Request.AppRelativeCurrentExecutionFilePath ?? "";
            litPortal.Text = path.StartsWith("~/Objections/", StringComparison.OrdinalIgnoreCase) ? "Answer Key Objection Portal"
                           : path.StartsWith("~/Marks/", StringComparison.OrdinalIgnoreCase) ? "Marks Objection Portal"
                           : path.StartsWith("~/Results/", StringComparison.OrdinalIgnoreCase) ? "OMR Result Portal"
                           : "Examination Portal";


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
            KPSCExamPortal.Security.AuthSession.SignOut(Context);

            Response.Redirect("~/Default.aspx", true);
        }
    }
}