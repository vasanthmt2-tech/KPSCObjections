using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace KPSCObjections
{
    public partial class SiteMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
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
            KPSCObjections.Security.AuthSession.SignOut(Context);

            Response.Redirect("~/Login.aspx", true);
        }
    }
}