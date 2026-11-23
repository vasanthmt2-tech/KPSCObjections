using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.SessionState;
using KPSCMarksObjection.DLL;
using KPSCMarksObjection.Security;

namespace KPSCMarksObjection
{
    /// <summary>
    /// Streams a marks objection's supporting PDF to the signed-in candidate
    /// who uploaded it. The URL carries only a random per-session token
    /// (DocumentReferences.MarksUrl); the registration number and the
    /// examination always come from the session.
    /// </summary>
    public class MarksDocument : IHttpHandler, IReadOnlySessionState
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

            DocumentReferences.Target target =
                DocumentReferences.Resolve(context.Session, context.Request.QueryString["d"]);

            if (target == null || target.PaperCode == null ||
                !target.PaperCode.StartsWith(DocumentReferences.MarksPrefix, StringComparison.Ordinal))
            {
                AppLog.Write("WARN", "Marks document request with an unknown reference by " + candidate.RegNo +
                             " from " + RequestThrottle.ClientIp(context));
                context.Response.StatusCode = 404;
                return;
            }

            string paper = target.PaperCode.Substring(DocumentReferences.MarksPrefix.Length);

            DataTable dt = new SqlHelper().ExecuteDataTable(
                "SELECT Document FROM dbo.MarksObjection " +
                "WHERE Regno = @RegNo AND ExamCode = @ExamCode AND PaperCode = @PaperCode",
                new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = candidate.RegNo },
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = candidate.ExamCode },
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paper });

            if (dt.Rows.Count == 0 || dt.Rows[0]["Document"] == DBNull.Value)
            {
                context.Response.StatusCode = 404;
                return;
            }

            byte[] data;

            try
            {
                data = PdfSanitizer.Sanitize((byte[])dt.Rows[0]["Document"]).Pdf;
            }
            catch (Exception ex)
            {
                AppLog.Error("Stored PDF could not be cleaned, not served: " + ex);
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = "application/pdf";
            context.Response.AddHeader("Content-Disposition",
                "inline; filename=\"MarksObjection_" + paper + ".pdf\"");
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.BinaryWrite(data);
        }
    }
}
