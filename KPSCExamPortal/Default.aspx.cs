using System;
using KPSCExamPortal.Security;

namespace KPSCExamPortal
{
    /// <summary>
    /// Home page: the three candidate services. One OTP sign-in covers all of
    /// them; once signed in, each card goes straight to its service.
    /// </summary>
    public partial class _Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            bool signedIn = AuthSession.IsCandidate(Context);

            // The answer key objection module can be switched off (see PortalModules).
            cardKey.Visible = PortalModules.KeyAnswerObjections;

            lnkKey.HRef = signedIn ? "~/Objections/Objection.aspx" : "~/Login.aspx";
            lnkKey.InnerText = signedIn ? "File objections" : "Sign in with OTP";

            lnkMarks.HRef = signedIn ? "~/Marks/Objection.aspx" : "~/Login.aspx";
            lnkMarks.InnerText = signedIn ? "Marks objection" : "Sign in with OTP";

            lnkResult.HRef = signedIn ? "~/Results/Result.aspx" : "~/Login.aspx";
            lnkResult.InnerText = signedIn ? "See my result" : "Sign in with OTP";
        }
    }
}
