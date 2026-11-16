using System;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI.WebControls;
using KPSCObjections.DLL;

namespace KPSCObjections
{
    /// <summary>
    /// Admin page listing the objections submitted by candidates (dbo.Objections),
    /// with the number of objections. All are listed by default; searching a
    /// registration number lists and counts that candidate's only.
    ///
    /// Only paid objections count as submitted: drafts (not paid) and those
    /// whose payment is still being verified or failed are left out.
    /// </summary>
    public partial class CandidateObjections : KPSCObjections.Security.AdminPage
    {
        private const string PaidOnly =
            @" INNER JOIN dbo.PaymentTransactions p
                       ON p.PaymentId = o.PaymentId AND p.PaymentStatus = 'SUCCESS'";

        private static readonly Regex RegNoPattern = new Regex(@"^[A-Za-z0-9]{1,10}$");

        private readonly SqlHelper sh = new SqlHelper();

        /// <summary>
        /// The registration number currently searched ("" = all). Kept in
        /// ViewState (signed and encrypted), so paging stays on the same search
        /// even if the box is edited without pressing Search.
        /// </summary>
        private string SearchedRegNo
        {
            get { return ViewState["SearchedRegNo"] as string ?? ""; }
            set { ViewState["SearchedRegNo"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
                BindObjections();
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            string regNo = txtRegNo.Text.Trim();

            if (regNo.Length == 0)
            {
                ShowAll();
                return;
            }

            if (!RegNoPattern.IsMatch(regNo))
            {
                ShowSummary("Please enter a valid registration number (letters and digits only, up to 10).", false);
                txtRegNo.Focus();
                return;
            }

            SearchedRegNo = regNo;
            gvObjections.PageIndex = 0;
            BindObjections();

            // Looking up one candidate's objections is recorded; the full list is not.
            KPSCObjections.Security.AdminAudit.Record(Context,
                KPSCObjections.Security.AdminAudit.ViewCandidateObjections, "Reg No " + regNo);
        }

        protected void btnShowAll_Click(object sender, EventArgs e)
        {
            ShowAll();
        }

        protected void gvObjections_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvObjections.PageIndex = e.NewPageIndex;
            BindObjections();
        }

        private void ShowAll()
        {
            txtRegNo.Text = "";
            SearchedRegNo = "";
            gvObjections.PageIndex = 0;
            BindObjections();
        }

        private void BindObjections()
        {
            string regNo = SearchedRegNo;

            DataTable dt = sh.ExecuteDataTable(
                @"SELECT o.Regno, o.PaperCode, o.VersionCode, o.QuestionNo, o.KeyAnswer,
                         o.ObjectionAnswer, o.CategoryCode, o.Remarks
                  FROM dbo.Objections o" + PaidOnly + @"
                  WHERE @RegNo = '' OR o.Regno = @RegNo
                  ORDER BY o.Regno, o.PaperCode, o.QuestionNo",
                new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = regNo });

            gvObjections.DataSource = dt;
            gvObjections.DataBind();

            int objections = dt.Rows.Count;

            if (regNo.Length > 0)
            {
                ShowSummary(objections == 0
                    ? "No submitted (paid) objections found for registration number " + regNo + "."
                    : "Registration number " + regNo + " has submitted " + Plural(objections, "objection") + ".",
                    objections > 0);
                return;
            }

            int candidates = 0;

            if (objections > 0)
            {
                candidates = Convert.ToInt32(sh.ExecuteScalar(
                    "SELECT COUNT(DISTINCT o.Regno) FROM dbo.Objections o" + PaidOnly));
            }

            ShowSummary(Plural(objections, "objection") + " submitted (paid) by " +
                        Plural(candidates, "candidate") + ".", true);
        }

        private static string Plural(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? "" : "s");
        }

        private void ShowSummary(string message, bool ok)
        {
            lblSummary.Text = HttpUtility.HtmlEncode(message);
            lblSummary.CssClass = ok ? "fw-bold" : "fw-bold text-danger";
        }
    }
}
