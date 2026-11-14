using KPSCObjections.DLL;
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
            DataTable dt = sh.ExecuteDataTable(@"SELECT
    o.PaperCode AS SubjectCode,
    s.SubjectDescription AS SubjectDesc,

    CASE UPPER(LTRIM(RTRIM(
        CASE
            WHEN o.PaperCode = 'P1' THEN c.P1VersionCode
            WHEN o.PaperCode = 'P2' THEN c.P2VersionCode
        END
    )))
        WHEN 'A' THEN ka.SetA
        WHEN 'B' THEN ka.SetB
        WHEN 'C' THEN ka.SetC
        WHEN 'D' THEN ka.SetD
        WHEN 'E' THEN ka.SetE
        WHEN 'F' THEN ka.SetF
        WHEN 'G' THEN ka.SetG
        WHEN 'H' THEN ka.SetH
        WHEN 'I' THEN ka.SetI
        WHEN 'J' THEN ka.SetJ
        WHEN 'K' THEN ka.SetK
        WHEN 'L' THEN ka.SetL
    END AS QuestionNo,

    COUNT(*) AS NoOfObjections

FROM dbo.Objections o

INNER JOIN dbo.PaymentTransactions p
    ON p.PaymentId = o.PaymentId
    AND p.PaymentStatus = 'SUCCESS'

INNER JOIN dbo.Candidate c
    ON c.RegNo = o.RegNo

LEFT JOIN dbo.Subject s
    ON o.PaperCode = s.PaperCode

LEFT JOIN dbo.KeyAnswers ka
    ON ka.ExamCode = c.ExamCode
    AND ka.SubjectCode = o.PaperCode
    AND ka.QuestionNo = o.QuestionNo

GROUP BY
    o.PaperCode,
    s.SubjectDescription,
    CASE UPPER(LTRIM(RTRIM(
        CASE
            WHEN o.PaperCode = 'P1' THEN c.P1VersionCode
            WHEN o.PaperCode = 'P2' THEN c.P2VersionCode
        END
    )))
        WHEN 'A' THEN ka.SetA
        WHEN 'B' THEN ka.SetB
        WHEN 'C' THEN ka.SetC
        WHEN 'D' THEN ka.SetD
        WHEN 'E' THEN ka.SetE
        WHEN 'F' THEN ka.SetF
        WHEN 'G' THEN ka.SetG
        WHEN 'H' THEN ka.SetH
        WHEN 'I' THEN ka.SetI
        WHEN 'J' THEN ka.SetJ
        WHEN 'K' THEN ka.SetK
        WHEN 'L' THEN ka.SetL
    END

ORDER BY
    o.PaperCode,
    QuestionNo;");

            gvObjectionSummary.DataSource = dt;
            gvObjectionSummary.DataBind();
        }
        private void DownloadObjectionDocuments(string subjectCode, string questionNo)
        {

            //string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

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
      CASE UPPER(LTRIM(RTRIM(o.VersionCode)))
          WHEN 'A' THEN TRY_CONVERT(INT, ka.SetA)
          WHEN 'B' THEN TRY_CONVERT(INT, ka.SetB)
          WHEN 'C' THEN TRY_CONVERT(INT, ka.SetC)
          WHEN 'D' THEN TRY_CONVERT(INT, ka.SetD)
          WHEN 'E' THEN TRY_CONVERT(INT, ka.SetE)
          WHEN 'F' THEN TRY_CONVERT(INT, ka.SetF)
          WHEN 'G' THEN TRY_CONVERT(INT, ka.SetG)
          WHEN 'H' THEN TRY_CONVERT(INT, ka.SetH)
          WHEN 'I' THEN TRY_CONVERT(INT, ka.SetI)
          WHEN 'J' THEN TRY_CONVERT(INT, ka.SetJ)
          WHEN 'K' THEN TRY_CONVERT(INT, ka.SetK)
          WHEN 'L' THEN TRY_CONVERT(INT, ka.SetL)
      END
ORDER BY o.VersionCode;", "@PaperCode,@MasterQuestionNo", "@" + subjectCode + ",@" + questionNo))
                        {       
                            int count = 0;

                            while (dr.Read())
                            {
                                if (dr["ObjectionDocs"] == DBNull.Value)
                                    continue;

                                byte[] pdfData = (byte[])dr["ObjectionDocs"];

                                string fileName =
                                    dr["FileName"] == DBNull.Value ? "Objection_" + (++count) + ".pdf" : Path.GetFileName(dr["FileName"].ToString());

                                if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                {
                                    fileName += ".pdf";
                                }

                                ZipArchiveEntry entry = zip.CreateEntry(fileName);

                                using (Stream entryStream = entry.Open())
                                {
                                    entryStream.Write(pdfData, 0, pdfData.Length);
                                }
                                count++;
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Payment re-verification failed: " + ex);
                lblVerifyResult.Text = "Payments could not be re-checked. Please try again later.";
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

                DownloadObjectionDocuments(subjectCode, questionNo);
            }
            else if (e.CommandName == "DownloadExcel")
            {
                int rowIndex = Convert.ToInt32(e.CommandArgument);

                string subjectCode =
                    gvObjectionSummary.DataKeys[rowIndex].Values["SubjectCode"].ToString();

                string questionNo =
                    gvObjectionSummary.DataKeys[rowIndex].Values["QuestionNo"].ToString();

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
      CASE UPPER(LTRIM(RTRIM(o.VersionCode)))
          WHEN 'A' THEN TRY_CONVERT(INT, ka.SetA)
          WHEN 'B' THEN TRY_CONVERT(INT, ka.SetB)
          WHEN 'C' THEN TRY_CONVERT(INT, ka.SetC)
          WHEN 'D' THEN TRY_CONVERT(INT, ka.SetD)
          WHEN 'E' THEN TRY_CONVERT(INT, ka.SetE)
          WHEN 'F' THEN TRY_CONVERT(INT, ka.SetF)
          WHEN 'G' THEN TRY_CONVERT(INT, ka.SetG)
          WHEN 'H' THEN TRY_CONVERT(INT, ka.SetH)
          WHEN 'I' THEN TRY_CONVERT(INT, ka.SetI)
          WHEN 'J' THEN TRY_CONVERT(INT, ka.SetJ)
          WHEN 'K' THEN TRY_CONVERT(INT, ka.SetK)
          WHEN 'L' THEN TRY_CONVERT(INT, ka.SetL)
      END
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

            StringBuilder html = new StringBuilder();

            html.Append("<table border='1'>");
            html.Append("<tr>");

            foreach (DataColumn col in dt.Columns)
            {
                html.Append("<th>");
                html.Append(HttpUtility.HtmlEncode(col.ColumnName));
                html.Append("</th>");
            }

            html.Append("</tr>");

            foreach (DataRow dr in dt.Rows)
            {
                html.Append("<tr>");

                foreach (DataColumn col in dt.Columns)
                {
                    html.Append("<td>");
                    html.Append(HttpUtility.HtmlEncode(
                        Convert.ToString(dr[col])));
                    html.Append("</td>");
                }

                html.Append("</tr>");
            }

            html.Append("</table>");

            Response.Clear();
            Response.Buffer = true;
            Response.ContentType = "application/vnd.ms-excel";

            Response.AddHeader(
                "Content-Disposition",
                "attachment;filename=ObjectionDetails_" +
                subjectCode + "_" + questionNo + ".xls"
            );

            Response.ContentEncoding = Encoding.UTF8;

            Response.Write("<html><head><meta charset='utf-8'></head><body>");
            Response.Write(html.ToString());
            Response.Write("</body></html>");

            Response.Flush();
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
    }
}