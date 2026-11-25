using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.UI.WebControls;
using KPSCExamPortal.DLL;
using KPSCExamPortal.Security;

namespace KPSCExamPortal.Admin
{
    /// <summary>
    /// Admin page showing the audit trail (dbo.AdminAudit, written by
    /// Security\AdminAudit.cs): who did what, when and from which IP address.
    /// Filter by date range, admin and action; download the filtered list as
    /// Excel. The page only reads the table; the database refuses changes to it.
    /// </summary>
    public partial class AuditTrail : AdminPage
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string Any = "";

        /// <summary>Most rows shown or downloaded for one filter.</summary>
        private const int MaxRows = 20000;

        private readonly SqlHelper sh = new SqlHelper();

        /// <summary>
        /// The filter last applied with Show. Kept in ViewState (signed and
        /// encrypted), so paging and Download Excel use what is listed even if
        /// the boxes were edited without pressing Show.
        /// </summary>
        [Serializable]
        private sealed class Filter
        {
            public DateTime From;
            public DateTime To;
            public string Admin;
            public string Action;
        }

        private Filter Applied
        {
            get { return ViewState["AuditFilter"] as Filter; }
            set { ViewState["AuditFilter"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindChoices();
                ResetToLastWeek();
            }
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            Filter filter = ReadFilter();

            if (filter == null)
                return;

            Applied = filter;
            gvAudit.PageIndex = 0;
            Bind();
        }

        protected void btnReset_Click(object sender, EventArgs e)
        {
            ResetToLastWeek();
        }

        protected void gvAudit_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvAudit.PageIndex = e.NewPageIndex;
            Bind();
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            Filter filter = Applied;

            if (filter == null)
                return;

            DataTable dt = LoadEntries(filter);

            if (dt.Rows.Count == 0)
            {
                ShowSummary("There are no entries to download for this filter.", false);
                Bind();
                return;
            }

            AdminAudit.Record(Context, AdminAudit.DownloadAuditTrail, Describe(filter) + ": " + dt.Rows.Count + " entries");

            using (ClosedXML.Excel.XLWorkbook workbook = new ClosedXML.Excel.XLWorkbook())
            using (MemoryStream ms = new MemoryStream())
            {
                ClosedXML.Excel.IXLWorksheet sheet = workbook.Worksheets.Add("Audit trail");
                string[] headers = { "Time", "Admin", "Action", "Details", "IP address" };

                for (int c = 0; c < headers.Length; c++)
                {
                    sheet.Cell(1, c + 1).SetValue(headers[c]);
                    sheet.Cell(1, c + 1).Style.Font.Bold = true;
                }

                // Every value is written as text, so nothing in Details can run as a formula.
                for (int r = 0; r < dt.Rows.Count; r++)
                {
                    DataRow row = dt.Rows[r];
                    sheet.Cell(r + 2, 1).SetValue(Convert.ToDateTime(row["AuditTime"]).ToString("dd-MMM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture));
                    sheet.Cell(r + 2, 2).SetValue(Convert.ToString(row["AdminUser"]));
                    sheet.Cell(r + 2, 3).SetValue(Convert.ToString(row["Action"]));
                    sheet.Cell(r + 2, 4).SetValue(Convert.ToString(row["Details"]));
                    sheet.Cell(r + 2, 5).SetValue(Convert.ToString(row["ClientIp"]));
                }

                sheet.SheetView.FreezeRows(1);
                sheet.Columns().Width = 22;
                sheet.Column(4).Width = 80;

                workbook.SaveAs(ms);

                Response.Clear();
                Response.Buffer = true;
                Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                Response.AddHeader("Content-Disposition",
                    "attachment;filename=AuditTrail_" + filter.From.ToString("yyyyMMdd", CultureInfo.InvariantCulture) +
                    "_" + filter.To.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".xlsx");
                Response.BinaryWrite(ms.ToArray());
                Response.Flush();

                // Stop the page's own HTML being appended to the downloaded file.
                Response.SuppressContent = true;
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        private void ResetToLastWeek()
        {
            DateTime today = DateTime.Today;

            txtFrom.Text = today.AddDays(-6).ToString(DateFormat, CultureInfo.InvariantCulture);
            txtTo.Text = today.ToString(DateFormat, CultureInfo.InvariantCulture);
            ddlAdmin.SelectedValue = Any;
            ddlAction.SelectedValue = Any;

            Applied = ReadFilter();
            gvAudit.PageIndex = 0;
            Bind();
        }

        private void BindChoices()
        {
            Fill(ddlAdmin, "All admins", "SELECT DISTINCT AdminUser FROM dbo.AdminAudit ORDER BY AdminUser");
            Fill(ddlAction, "All actions", "SELECT DISTINCT Action FROM dbo.AdminAudit ORDER BY Action");
        }

        private void Fill(DropDownList list, string allText, string query)
        {
            list.Items.Clear();
            list.Items.Add(new ListItem(allText, Any));

            foreach (DataRow row in sh.ExecuteDataTable(query).Rows)
            {
                string value = Convert.ToString(row[0]);
                list.Items.Add(new ListItem(value, value));
            }
        }

        /// <summary>The filter in the boxes, or null (with a message) when the dates are not valid.</summary>
        private Filter ReadFilter()
        {
            DateTime from, to;

            if (!DateTime.TryParseExact(txtFrom.Text.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out from) ||
                !DateTime.TryParseExact(txtTo.Text.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out to) ||
                from.Year < 2000 || to.Year > 2100)
            {
                ShowSummary("Please enter valid From and To dates.", false);
                return null;
            }

            if (to < from)
            {
                ShowSummary("The To date must not be before the From date.", false);
                return null;
            }

            return new Filter
            {
                From = from.Date,
                To = to.Date,
                Admin = ddlAdmin.SelectedValue,
                Action = ddlAction.SelectedValue
            };
        }

        private DataTable LoadEntries(Filter filter)
        {
            return sh.ExecuteDataTable(
                @"SELECT TOP (@Max) AuditTime, AdminUser, Action, Details, ClientIp
                  FROM dbo.AdminAudit
                  WHERE AuditTime >= @From AND AuditTime < @ToExclusive
                    AND (@Admin = '' OR AdminUser = @Admin)
                    AND (@Action = '' OR Action = @Action)
                  ORDER BY AuditTime DESC, AuditId DESC",
                new SqlParameter("@Max", SqlDbType.Int) { Value = MaxRows + 1 },
                new SqlParameter("@From", SqlDbType.DateTime2) { Value = filter.From },
                new SqlParameter("@ToExclusive", SqlDbType.DateTime2) { Value = filter.To.AddDays(1) },
                new SqlParameter("@Admin", SqlDbType.VarChar, 50) { Value = filter.Admin ?? "" },
                new SqlParameter("@Action", SqlDbType.VarChar, 50) { Value = filter.Action ?? "" });
        }

        private void Bind()
        {
            Filter filter = Applied;

            if (filter == null)
            {
                gvAudit.DataSource = null;
                gvAudit.DataBind();
                return;
            }

            DataTable dt = LoadEntries(filter);
            bool cut = dt.Rows.Count > MaxRows;

            if (cut)
                dt.Rows.RemoveAt(dt.Rows.Count - 1);

            gvAudit.DataSource = dt;
            gvAudit.DataBind();

            ShowSummary(dt.Rows.Count + (dt.Rows.Count == 1 ? " entry" : " entries") + ", " + Describe(filter) +
                (cut ? ". Only the latest " + MaxRows + " are shown; narrow the filter to see older ones." : "."), true);
        }

        private static string Describe(Filter filter)
        {
            return filter.From.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) + " to " +
                   filter.To.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) +
                   (string.IsNullOrEmpty(filter.Admin) ? "" : ", admin " + filter.Admin) +
                   (string.IsNullOrEmpty(filter.Action) ? "" : ", action " + filter.Action);
        }

        private void ShowSummary(string message, bool ok)
        {
            lblSummary.Text = HttpUtility.HtmlEncode(message);
            lblSummary.CssClass = ok ? "fw-bold" : "fw-bold text-danger";
        }
    }
}
