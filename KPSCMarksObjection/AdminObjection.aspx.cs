using KPSCMarksObjection.DLL;
using KPSCMarksObjection.Security;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Web;
using System.Web.UI.WebControls;

namespace KPSCMarksObjection
{
    /// <summary>
    /// Admin view of the marks objections (dbo.MarksObjection): every
    /// objection, 50 to a page with a full Excel download, and a count per
    /// examination and subject with that subject's Excel and documents ZIP.
    /// There is no payment, so every row is a submitted objection.
    /// </summary>
    public partial class AdminObjection : KPSCMarksObjection.Security.AdminPage
    {
        private readonly SqlHelper sh = new SqlHelper();

        protected void Page_Load(object sender, EventArgs e)
        {
        }

        // The paper's name and the examination come from the objection's
        // examination: paper codes (P1, P2) repeat in every examination.
        private const string DetailsFrom = @"
            FROM dbo.MarksObjection m
            LEFT JOIN dbo.Exam e ON e.ExamCode = m.ExamCode
            LEFT JOIN dbo.Subject s ON s.PaperCode = m.PaperCode AND s.ExamCode = m.ExamCode";

        private const string DetailsColumns = @"
            SELECT ISNULL(e.ExamDescription, m.ExamCode) AS ExamDesc,
                   m.PaperCode AS SubjectCode, s.SubjectDescription AS SubjectDesc, m.RegNo, m.VersionCode,
                   m.Remarks, m.FileName AS SupportingDocument,
                   CONVERT(varchar(10), m.SubmittedDate, 103) + ' ' + CONVERT(varchar(5), m.SubmittedDate, 108) AS SubmittedDate";

        private const string DetailsOrder = " ORDER BY m.ExamCode, m.PaperCode, m.RegNo";

        private static readonly string[] ExcelHeaders =
            { "Examination", "Paper", "Subject", "Reg. No.", "Version", "Remarks", "Document", "Submitted Date" };

        private static readonly string[] ExcelColumns =
            { "ExamDesc", "SubjectCode", "SubjectDesc", "RegNo", "VersionCode", "Remarks", "SupportingDocument", "SubmittedDate" };

        // ---------------------------------------------------------------
        // Objection Details
        // ---------------------------------------------------------------

        /// <summary>
        /// Shows one page (50 rows) of the objection details. Only that page is
        /// read and kept in ViewState; the full list is in Download all as Excel.
        /// </summary>
        public void LoadObjectionDetails()
        {
            int total = Convert.ToInt32(sh.ExecuteScalar("SELECT COUNT(*) " + DetailsFrom));
            int pages = Math.Max(1, (total + gvObjections.PageSize - 1) / gvObjections.PageSize);

            if (gvObjections.PageIndex >= pages)
                gvObjections.PageIndex = pages - 1;

            int skip = gvObjections.PageIndex * gvObjections.PageSize;

            DataTable dt = sh.ExecuteDataTable(
                DetailsColumns + DetailsFrom + DetailsOrder +
                " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
                new SqlParameter("@Skip", SqlDbType.Int) { Value = skip },
                new SqlParameter("@Take", SqlDbType.Int) { Value = gvObjections.PageSize });

            gvObjections.VirtualItemCount = total;
            gvObjections.DataSource = dt;
            gvObjections.DataBind();

            lblDetailsCount.Text = HttpUtility.HtmlEncode(total == 0
                ? "No submitted objections."
                : total.ToString("N0") + " submitted objection" + (total == 1 ? "" : "s") +
                  (total > gvObjections.PageSize
                      ? ", showing " + (skip + 1).ToString("N0") + " to " + (skip + dt.Rows.Count).ToString("N0")
                      : "") + ".");
            btnDetailsExcel.Visible = total > 0;
        }

        protected void gvObjections_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvObjections.PageIndex = e.NewPageIndex;
            LoadObjectionDetails();
            SetActiveButton("Details");
        }

        /// <summary>Every submitted objection, all examinations, as one Excel file.</summary>
        protected void btnDetailsExcel_Click(object sender, EventArgs e)
        {
            DataTable dt = sh.ExecuteDataTable(DetailsColumns + DetailsFrom + DetailsOrder);

            AdminAudit.Record(Context, AdminAudit.DownloadObjectionsExcel,
                "All marks objection details: " + dt.Rows.Count + " objections");

            SendExcel(dt, "MarksObjections_All_" + DateTime.Now.ToString("yyyyMMdd_HHmm", System.Globalization.CultureInfo.InvariantCulture));
        }

        // ---------------------------------------------------------------
        // Subject Wise Objections
        // ---------------------------------------------------------------

        private void BindObjectionSummary()
        {
            gvObjectionSummary.DataSource = sh.ExecuteDataTable(@"
                SELECT m.ExamCode,
                       ISNULL(e.ExamDescription, m.ExamCode) AS ExamDesc,
                       m.PaperCode AS SubjectCode,
                       ISNULL(s.SubjectDescription, m.PaperCode) AS SubjectDesc,
                       COUNT(*) AS NoOfObjections,
                       SUM(CASE WHEN m.Document IS NULL THEN 0 ELSE 1 END) AS NoOfDocuments
                FROM dbo.MarksObjection m
                LEFT JOIN dbo.Exam e ON e.ExamCode = m.ExamCode
                LEFT JOIN dbo.Subject s ON s.PaperCode = m.PaperCode AND s.ExamCode = m.ExamCode
                GROUP BY m.ExamCode, e.ExamDescription, m.PaperCode, s.SubjectDescription
                ORDER BY m.ExamCode, m.PaperCode");

            gvObjectionSummary.DataBind();
        }

        protected void gvObjectionSummary_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "DownloadZip" && e.CommandName != "DownloadExcel")
                return;

            int index = Convert.ToInt32(e.CommandArgument);
            string examCode = gvObjectionSummary.DataKeys[index].Values["ExamCode"].ToString();
            string subjectCode = gvObjectionSummary.DataKeys[index].Values["SubjectCode"].ToString();

            if (e.CommandName == "DownloadZip")
            {
                AdminAudit.Record(Context, AdminAudit.DownloadObjectionsZip,
                    "Marks objections: exam " + examCode + ", paper " + subjectCode);

                DownloadObjectionDocuments(examCode, subjectCode);
            }
            else
            {
                AdminAudit.Record(Context, AdminAudit.DownloadObjectionsExcel,
                    "Marks objections: exam " + examCode + ", paper " + subjectCode);

                DataTable dt = sh.ExecuteDataTable(
                    DetailsColumns + DetailsFrom +
                    " WHERE m.ExamCode = @ExamCode AND m.PaperCode = @PaperCode ORDER BY m.RegNo",
                    new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode },
                    new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = subjectCode });

                if (dt.Rows.Count == 0)
                    return;

                SendExcel(dt, "MarksObjections_Exam" + examCode + "_" + subjectCode);
            }
        }

        /// <summary>The subject's supporting PDFs as one ZIP, each named RegNo_FileName.</summary>
        private void DownloadObjectionDocuments(string examCode, string subjectCode)
        {
            // Documents that could not be cleaned are left out and listed in the zip.
            List<string> skipped = new List<string>();

            using (MemoryStream ms = new MemoryStream())
            {
                using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    using (SqlDataReader dr = sh.ExecuteDataReader(
                        @"SELECT RegNo, FileName, Document
                          FROM dbo.MarksObjection
                          WHERE ExamCode = @ExamCode AND PaperCode = @PaperCode AND Document IS NOT NULL
                          ORDER BY RegNo",
                        new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode },
                        new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = subjectCode }))
                    {
                        while (dr.Read())
                        {
                            byte[] pdfData;

                            try
                            {
                                // Cleaned again before it leaves the server.
                                pdfData = PdfSanitizer.Sanitize((byte[])dr["Document"]).Pdf;
                            }
                            catch (Exception ex)
                            {
                                // Our own reasons (page limit, empty file) are safe to list;
                                // anything else is logged and summarised.
                                skipped.Add(dr["RegNo"] + " (" + (ex is InvalidDataException
                                    ? ex.Message
                                    : ErrorReport.Handle(ex, "Stored PDF of " + dr["RegNo"] + " could not be cleaned",
                                          "file is damaged or password-protected")) + ")");
                                continue;
                            }

                            string fileName = dr["FileName"] == DBNull.Value
                                ? "document.pdf"
                                : Path.GetFileName(dr["FileName"].ToString());

                            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                fileName += ".pdf";

                            // One objection per candidate and subject, so the
                            // registration number keeps every entry distinct.
                            ZipArchiveEntry entry = zip.CreateEntry(dr["RegNo"] + "_" + fileName);

                            using (Stream entryStream = entry.Open())
                            {
                                entryStream.Write(pdfData, 0, pdfData.Length);
                            }
                        }
                    }

                    if (skipped.Count > 0)
                    {
                        ZipArchiveEntry note = zip.CreateEntry("NOT_INCLUDED.txt");

                        using (StreamWriter writer = new StreamWriter(note.Open()))
                        {
                            writer.WriteLine("These documents could not be checked for harmful content and were left out:");
                            foreach (string s in skipped)
                                writer.WriteLine(s);
                        }
                    }
                }

                if (ms.Length == 0)
                    return;

                Response.Clear();
                Response.ContentType = "application/zip";
                Response.AddHeader("Content-Disposition",
                    "attachment; filename=MarksObjections_Exam" + examCode + "_" + subjectCode + ".zip");
                Response.BinaryWrite(ms.ToArray());
                Response.Flush();

                // Stop the page's own HTML being appended to the downloaded file.
                Response.SuppressContent = true;
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Sends the rows as an .xlsx file. Every value is written as text, so
        /// nothing a candidate typed can run as a formula.
        /// </summary>
        private void SendExcel(DataTable dt, string fileName)
        {
            using (ClosedXML.Excel.XLWorkbook workbook = new ClosedXML.Excel.XLWorkbook())
            using (MemoryStream ms = new MemoryStream())
            {
                ClosedXML.Excel.IXLWorksheet sheet = workbook.Worksheets.Add("Objections");

                for (int c = 0; c < ExcelHeaders.Length; c++)
                {
                    sheet.Cell(1, c + 1).SetValue(ExcelHeaders[c]);
                    sheet.Cell(1, c + 1).Style.Font.Bold = true;
                }

                for (int r = 0; r < dt.Rows.Count; r++)
                {
                    for (int c = 0; c < ExcelColumns.Length; c++)
                        sheet.Cell(r + 2, c + 1).SetValue(Convert.ToString(dt.Rows[r][ExcelColumns[c]]));
                }

                sheet.SheetView.FreezeRows(1);
                sheet.Columns().Width = 16;
                sheet.Column(Array.IndexOf(ExcelColumns, "Remarks") + 1).Width = 60;

                workbook.SaveAs(ms);

                Response.Clear();
                Response.Buffer = true;
                Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                Response.AddHeader("Content-Disposition", "attachment;filename=" + fileName + ".xlsx");
                Response.BinaryWrite(ms.ToArray());
                Response.Flush();

                // Stop the page's own HTML being appended to the downloaded file.
                Response.SuppressContent = true;
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        protected void lnkObjectionSummary_Click(object sender, EventArgs e)
        {
            divObjectionDetails.Visible = false;
            divObjectionSummary.Visible = true;

            BindObjectionSummary();

            SetActiveButton("Summary");
        }

        protected void lnkObjectionDetails_Click(object sender, EventArgs e)
        {
            divObjectionDetails.Visible = true;
            divObjectionSummary.Visible = false;

            LoadObjectionDetails();

            SetActiveButton("Details");
        }

        private void SetActiveButton(string selected)
        {
            if (selected == "Details")
            {
                lnkObjectionDetails.CssClass = "btn btn-primary me-2";
                lnkObjectionSummary.CssClass = "btn btn-outline-primary";
            }
            else
            {
                lnkObjectionDetails.CssClass = "btn btn-outline-primary me-2";
                lnkObjectionSummary.CssClass = "btn btn-primary";
            }
        }
    }
}
