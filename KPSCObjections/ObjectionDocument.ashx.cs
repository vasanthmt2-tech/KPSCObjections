using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.SessionState;
using KPSCObjections.DLL;
using KPSCObjections.Security;

namespace KPSCObjections
{
    /// <summary>
    /// Streams a supporting PDF to the signed-in candidate who uploaded it.
    /// The registration number comes from the session, never from the URL.
    /// </summary>
    public class ObjectionDocument : IHttpHandler, IReadOnlySessionState
    {
        public bool IsReusable { get { return true; } }

        public void ProcessRequest(HttpContext context)
        {
            if (!AuthSession.IsCandidate(context))
            {
                context.Response.StatusCode = 403;
                return;
            }

            ObjectionSession.LoginDetails candidate =
                (ObjectionSession.LoginDetails)context.Session[AuthSession.CandidateKey];

            string paper = context.Request.QueryString["paper"] ?? "";
            int questionNo;

            if (paper.Length == 0 || paper.Length > 2 ||
                !int.TryParse(context.Request.QueryString["q"], out questionNo))
            {
                context.Response.StatusCode = 404;
                return;
            }

            DataTable dt = new SqlHelper().ExecuteDataTable(
                "SELECT FileName, ObjectionDocs FROM dbo.ObjectionsDocument " +
                "WHERE Regno = @RegNo AND PaperCode = @PaperCode AND QuestionNo = @QuestionNo",
                new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = candidate.RegNo },
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paper },
                new SqlParameter("@QuestionNo", SqlDbType.Int) { Value = questionNo });

            if (dt.Rows.Count == 0 || dt.Rows[0]["ObjectionDocs"] == DBNull.Value)
            {
                context.Response.StatusCode = 404;
                return;
            }

            byte[] data;

            try
            {
                // Uploads are cleaned when saved; cleaning again here also covers
                // files stored before that was done.
                data = PdfSanitizer.Sanitize((byte[])dt.Rows[0]["ObjectionDocs"]).Pdf;
            }
            catch (Exception ex)
            {
                AppLog.Error("Stored PDF could not be cleaned, not served: " + ex);
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = "application/pdf";
            context.Response.AddHeader("Content-Disposition",
                "inline; filename=\"Objection_" + paper + "_" + questionNo + ".pdf\"");
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.BinaryWrite(data);
        }
    }
}
