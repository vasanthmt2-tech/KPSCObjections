using System;
using System.Web;
using System.Web.UI;
using KPSCObjections.Security;

namespace KPSCObjections
{
    public partial class AdminLogin : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && AuthSession.IsAdmin(Context))
            {
                Response.Redirect("~/UploadKeyAnswer.aspx", true);
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            string userName = txtEmpId.Text.Trim();
            string password = txtEmpPwd.Text;

            if (string.IsNullOrWhiteSpace(userName))
            {
                txtEmpId.Focus();
                Alert("Please enter the user name.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                txtEmpPwd.Focus();
                Alert("Please enter the Password.");
                return;
            }

            if (userName.Length > 50 || password.Length > 200)
            {
                Alert("Invalid username or password.");
                return;
            }

            AdminAuth.Result result;

            try
            {
                result = AdminAuth.Verify(userName, password);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Admin login failed: " + ex);
                Alert("Login is temporarily unavailable. Please try again later.");
                return;
            }

            switch (result)
            {
                case AdminAuth.Result.Ok:
                    AuthSession.SignInAdmin(Context, userName);
                    Response.Redirect("~/UploadKeyAnswer.aspx", true);
                    break;

                case AdminAuth.Result.LockedOut:
                    Alert("Too many failed attempts. Please try again after 15 minutes.");
                    break;

                default:
                    Alert("Invalid username or password.");
                    break;
            }
        }

        private void Alert(string message)
        {
            ScriptManager.RegisterStartupScript(
                this, GetType(), "alert",
                "alert('" + HttpUtility.JavaScriptStringEncode(message) + "');", true);
        }
    }
}
