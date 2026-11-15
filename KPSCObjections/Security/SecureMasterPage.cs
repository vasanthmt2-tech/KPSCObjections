using System;
using System.Web;
using System.Web.UI;

namespace KPSCObjections.Security
{
    /// <summary>
    /// Base class for the site's master pages. Protects every form post
    /// against cross-site request forgery (the standard WebForms scheme):
    ///
    ///  - a random token is kept in an HttpOnly cookie;
    ///  - the token keys the page's ViewState (ViewStateUserKey), so ViewState
    ///    issued to one browser is rejected when posted from another;
    ///  - the token and the signed-in user are also stored in ViewState and
    ///    compared on every postback.
    ///
    /// A forged post from another site cannot supply a matching ViewState, so
    /// it fails before any button handler runs.
    /// </summary>
    public class SecureMasterPage : MasterPage
    {
        private const string TokenCookie = "__AntiXsrfToken";
        private const string TokenKey = "__AntiXsrfToken";
        private const string UserKey = "__AntiXsrfUser";

        /// <summary>Recognised by Application_Error, which answers 400 instead of 500.</summary>
        public const string AntiXsrfFailedMessage = "Validation of Anti-XSRF token failed.";

        private string antiXsrfToken;

        protected override void OnInit(EventArgs e)
        {
            HttpCookie cookie = Request.Cookies[TokenCookie];
            Guid parsed;

            if (cookie != null && Guid.TryParse(cookie.Value, out parsed))
            {
                antiXsrfToken = cookie.Value;
            }
            else
            {
                antiXsrfToken = Guid.NewGuid().ToString("N");

                Response.Cookies.Set(new HttpCookie(TokenCookie, antiXsrfToken)
                {
                    HttpOnly = true,
                    Secure = Request.IsSecureConnection,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });
            }

            // Must be set during Init, before ViewState is loaded.
            Page.ViewStateUserKey = antiXsrfToken;
            Page.PreLoad += Page_PreLoad;

            base.OnInit(e);
        }

        private void Page_PreLoad(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ViewState[TokenKey] = antiXsrfToken;
                ViewState[UserKey] = CurrentUser();
            }
            else if ((string)ViewState[TokenKey] != antiXsrfToken
                  || (string)ViewState[UserKey] != CurrentUser())
            {
                throw new InvalidOperationException(AntiXsrfFailedMessage);
            }
        }

        /// <summary>Who is signed in, so a form opened by one user cannot be posted as another.</summary>
        private string CurrentUser()
        {
            if (Session == null)
                return "";

            return "c:" + Convert.ToString(Session["RegNo"]) +
                  "|a:" + Convert.ToString(Session[AuthSession.AdminKey]);
        }
    }
}
