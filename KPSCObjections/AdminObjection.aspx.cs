using KPSCObjections.DLL;
using KPSCObjections.Security;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace KPSCObjections
{
    public partial class AdminObjection : KPSCObjections.Security.AdminPage
    {
        SqlHelper sh = new SqlHelper();
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                //Databind();
            }
        }

        public void LoadObjectionDetails()
        {
            // Only objections whose payment is confirmed count as submitted.
            DataTable dt = sh.ExecuteDataTable(@"SELECT o.PaperCode as SubjectCode, SubjectDescription as SubjectDesc,o.RegNo,o.VersionCode,o.QuestionNo,KeyAnswer,ObjectionAnswer,Remarks, filename as SupportingDocument,
                            ISNULL(p.SBIReferenceNo, p.MerchantOrderNo) as TransactionID,CONVERT(varchar(10), ISNULL(p.PaymentDate, p.CreatedDate),103) SubmittedDate FROM Objections o
                            inner join PaymentTransactions p on p.PaymentId = o.PaymentId and p.PaymentStatus = 'SUCCESS'
                            inner join ObjectionsDocument d on o.regno = d.regno and o.PaperCode = d.PaperCode and o.QuestionNo = d.QuestionNo
                            left outer join Subject s on o.PaperCode = s.PaperCode
                            order by o.PaperCode, o.QuestionNo, o.RegNo");

            gvObjections.DataSource = dt;
            gvObjections.DataBind();
        }

        private void BindObjectionSummary()
        {
            // One row per MASTER question. An objection stores the question
            // number as printed in the candidate's own version, so it is mapped
            // back through that version's Set column (SetA..SetL, see Security\KeySets.cs) to the master
            // question. The Excel and ZIP downloads below take this master
            // question number and apply the same mapping.
            DataTable dt = sh.ExecuteDataTable(@"SELECT
    o.PaperCode AS SubjectCode,
    s.SubjectDescription AS SubjectDesc,
    ka.QuestionNo AS QuestionNo,
    COUNT(*) AS NoOfObjections

FROM dbo.Objections o

INNER JOIN dbo.PaymentTransactions p
    ON p.PaymentId = o.PaymentId
    AND p.PaymentStatus = 'SUCCESS'

INNER JOIN dbo.Candidate c
    ON c.RegNo = o.RegNo

LEFT JOIN dbo.Subject s
    ON o.PaperCode = s.PaperCode

INNER JOIN dbo.KeyAnswers ka
    ON ka.ExamCode = c.ExamCode
    AND ka.SubjectCode = o.PaperCode
    AND o.QuestionNo =
        " + KeySets.QuestionColumnSql("ka", "o.VersionCode", true) + @"

GROUP BY
    o.PaperCode,
    s.SubjectDescription,
    ka.QuestionNo

ORDER BY
    o.PaperCode,
    ka.QuestionNo;");

            gvObjectionSummary.DataSource = dt;
            gvObjectionSummary.DataBind();
        }
        private void DownloadObjectionDocuments(string subjectCode, string questionNo)
        {

            //string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

            // Documents that could not be cleaned are left out and listed in the zip.
            List<string> skipped = new List<string>();

            using (MemoryStream ms = new MemoryStream())
            {
                using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    using (SqlDataReader dr = sh.ExecuteDataReader(@"SELECT
    o.RegNo,
    o.PaperCode,
    o.VersionCode,
    o.QuestionNo AS CandidateQuestionNo,
    ka.QuestionNo AS MasterQuestionNo,
    o.KeyAnswer,
    o.ObjectionAnswer,
    d.FileName,
    d.ObjectionDocs
FROM dbo.Objections o
INNER JOIN dbo.PaymentTransactions p
    ON p.PaymentId = o.PaymentId
   AND p.PaymentStatus = 'SUCCESS'
INNER JOIN dbo.Candidate c
    ON c.RegNo = o.RegNo
INNER JOIN dbo.KeyAnswers ka
    ON ka.ExamCode = c.ExamCode
   AND ka.SubjectCode = o.PaperCode
   AND ka.QuestionNo = @MasterQuestionNo
INNER JOIN dbo.ObjectionsDocument d
    ON d.RegNo = o.RegNo
   AND d.PaperCode = o.PaperCode
   AND d.QuestionNo = o.QuestionNo
WHERE o.PaperCode = @PaperCode
  AND o.QuestionNo =
      " + KeySets.QuestionColumnSql("ka", "o.VersionCode", true) + @"
ORDER BY o.VersionCode;", "@PaperCode,@MasterQuestionNo", "@" + subjectCode + ",@" + questionNo))
                        {       
                            int count = 0;

                            while (dr.Read())
                            {
                                if (dr["ObjectionDocs"] == DBNull.Value)
                                    continue;

                                byte[] pdfData;

                                try
                                {
                                    // Cleaned again here so files stored before
                                    // upload cleaning existed are safe to open too.
                                    pdfData = PdfSanitizer.Sanitize((byte[])dr["ObjectionDocs"]).Pdf;
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

                                string fileName =
                                    dr["FileName"] == DBNull.Value ? "Objection_" + (++count) + ".pdf" : Path.GetFileName(dr["FileName"].ToString());

                                if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                {
                                    fileName += ".pdf";
                                }

                                // Two candidates can upload files with the same name;
                                // the registration number keeps every entry distinct.
                                fileName = dr["RegNo"] + "_" + fileName;

                                ZipArchiveEntry entry = zip.CreateEntry(fileName);

                                using (Stream entryStream = entry.Open())
                                {
                                    entryStream.Write(pdfData, 0, pdfData.Length);
                                }
                                count++;
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

                Response.AddHeader("Content-Disposition","attachment; filename=Subject_" + subjectCode + "_Question_" + questionNo + ".zip");

                Response.BinaryWrite(ms.ToArray());
                Response.Flush();

                // Stop the page's own HTML being appended to the downloaded file.
                Response.SuppressContent = true;

                HttpContext.Current.ApplicationInstance.CompleteRequest();
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

        /// <summary>
        /// Asks SBI ePay for the status of every payment still pending, for
        /// candidates whose payment response never reached the portal.
        /// </summary>
        protected void lnkVerifyPayments_Click(object sender, EventArgs e)
        {
            int succeeded, failed, stillPending;

            try
            {
                KPSCObjections.Payments.PaymentService.VerifyAllPending(
                    out succeeded, out failed, out stillPending);

                lblVerifyResult.Text =
                    "Pending payments re-checked: " + succeeded + " confirmed, " +
                    failed + " not completed, " + stillPending + " still pending.";

                AdminAudit.Record(Context, AdminAudit.RecheckPayments,
                    succeeded + " confirmed, " + failed + " not completed, " + stillPending + " still pending");
            }
            catch (Exception ex)
            {
                lblVerifyResult.Text = HttpUtility.HtmlEncode(ErrorReport.Handle(ex, "Payment re-verification failed",
                    "Payments could not be re-checked. Please try again later."));
            }
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
        protected void gvObjectionSummary_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "DownloadZip")
            {
                int index = Convert.ToInt32(e.CommandArgument);

                string subjectCode =
                    gvObjectionSummary.DataKeys[index].Values["SubjectCode"].ToString();

                string questionNo =
                    gvObjectionSummary.DataKeys[index].Values["QuestionNo"].ToString();

                AdminAudit.Record(Context, AdminAudit.DownloadObjectionsZip,
                    "Paper " + subjectCode + ", question " + questionNo);

                DownloadObjectionDocuments(subjectCode, questionNo);
            }
            else if (e.CommandName == "DownloadExcel")
            {
                int rowIndex = Convert.ToInt32(e.CommandArgument);

                string subjectCode =
                    gvObjectionSummary.DataKeys[rowIndex].Values["SubjectCode"].ToString();

                string questionNo =
                    gvObjectionSummary.DataKeys[rowIndex].Values["QuestionNo"].ToString();

                AdminAudit.Record(Context, AdminAudit.DownloadObjectionsExcel,
                    "Paper " + subjectCode + ", question " + questionNo);

                DownloadObjectionExcel(subjectCode, questionNo);
            }
        }
        private void DownloadObjectionExcel(string subjectCode, string questionNo)
        {
            //ObjectionSession.LoginDetails ssen = (ObjectionSession.LoginDetails)Session["LoginValid"];

            //if (Session["LoginValid"] == null)
            //    return;

            string query = @"SELECT
    s.SubjectCode,
    o.RegNo,
    o.VersionCode AS CandidateVersionCode,
    ka.QuestionNo AS MasterQuestionNo,
    o.QuestionNo AS CandidateQuestionNo,
    ka.MasterAnswer AS KeyAnswer,
    o.ObjectionAnswer,
    o.Remarks
FROM dbo.Objections o
INNER JOIN dbo.PaymentTransactions p
    ON p.PaymentId = o.PaymentId
   AND p.PaymentStatus = 'SUCCESS'
INNER JOIN dbo.Candidate c
    ON c.RegNo = o.RegNo
INNER JOIN dbo.Subject s
    ON s.PaperCode = o.PaperCode
INNER JOIN dbo.KeyAnswers ka
    ON ka.ExamCode = c.ExamCode
   AND ka.SubjectCode = o.PaperCode
   AND ka.QuestionNo = @QuestionNo
WHERE o.PaperCode = @PaperCode
  AND o.QuestionNo =
      " + KeySets.QuestionColumnSql("ka", "o.VersionCode", true) + @"
ORDER BY s.SubjectCode, ka.QuestionNo, o.VersionCode;";

            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Objection_ConnectionString"].ConnectionString;

            DataTable dt = new DataTable();

            using (SqlConnection con = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, con))
            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
            {
                //cmd.Parameters.AddWithValue("@RegNo", ssen.RegNo);
                cmd.Parameters.AddWithValue("@PaperCode", subjectCode);
                cmd.Parameters.AddWithValue("@QuestionNo", questionNo);

                da.Fill(dt);
            }

            if (dt.Rows.Count == 0)
                return;

            // A real .xlsx workbook. Cell values are written as text, so a remark
            // that starts with "=" is shown as typed and never runs as a formula.
            using (ClosedXML.Excel.XLWorkbook workbook = new ClosedXML.Excel.XLWorkbook())
            using (MemoryStream ms = new MemoryStream())
            {
                ClosedXML.Excel.IXLWorksheet sheet = workbook.Worksheets.Add("Objections");

                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    sheet.Cell(1, c + 1).SetValue(dt.Columns[c].ColumnName);
                    sheet.Cell(1, c + 1).Style.Font.Bold = true;
                }

                for (int r = 0; r < dt.Rows.Count; r++)
                {
                    for (int c = 0; c < dt.Columns.Count; c++)
                    {
                        sheet.Cell(r + 2, c + 1).SetValue(Convert.ToString(dt.Rows[r][c]));
                    }
                }

                sheet.SheetView.FreezeRows(1);
                sheet.Columns().Width = 18;
                sheet.Column(dt.Columns.Count).Width = 60;   // Remarks

                workbook.SaveAs(ms);

                Response.Clear();
                Response.Buffer = true;
                Response.ContentType =
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                Response.AddHeader(
                    "Content-Disposition",
                    "attachment;filename=ObjectionDetails_" +
                    subjectCode + "_" + questionNo + ".xlsx"
                );

                Response.BinaryWrite(ms.ToArray());
            }

            Response.Flush();

            // Stop the page's own HTML being appended to the downloaded file.
            Response.SuppressContent = true;
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
    }
}
