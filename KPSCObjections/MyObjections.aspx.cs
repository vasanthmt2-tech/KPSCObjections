using KPSCObjections.DLL;
using KPSCObjections.Payments;
using KPSCObjections.Security;
using System;
using System.Data;
using System.Data.SqlClient;

namespace KPSCObjections
{
    /// <summary>
    /// Objections the candidate has paid for, or whose payment is still being
    /// verified. Unpaid drafts stay on the File Objection page.
    /// </summary>
    public partial class MyObjections : KPSCObjections.Security.CandidatePage
    {
        private readonly SqlHelper sh = new SqlHelper();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack)
                return;

            ObjectionSession.LoginDetails candidate =
                (ObjectionSession.LoginDetails)Session[AuthSession.CandidateKey];

            // Pick up payments whose response never reached us.
            PaymentService.VerifyCandidatePending(candidate.RegNo);

            gvObjections.DataSource = sh.ExecuteDataTable(
                @"SELECT o.PaperCode, s.SubjectDescription AS SubjectCode, o.VersionCode, o.QuestionNo,
                         o.KeyAnswer, o.ObjectionAnswer, o.Remarks, d.FileName AS SupportingDocument,
                         ISNULL(p.SBIReferenceNo, p.MerchantOrderNo) AS TransactionId,
                         CONVERT(varchar(10), ISNULL(p.PaymentDate, p.CreatedDate), 103) AS SubmittedDate,
                         CASE p.PaymentStatus WHEN 'SUCCESS' THEN 'Submitted'
                                              ELSE 'Payment being verified' END AS Status
                  FROM dbo.Objections o
                  INNER JOIN dbo.PaymentTransactions p
                          ON p.PaymentId = o.PaymentId AND p.PaymentStatus IN ('SUCCESS', 'PENDING')
                  LEFT JOIN dbo.ObjectionsDocument d
                         ON d.Regno = o.Regno AND d.PaperCode = o.PaperCode AND d.QuestionNo = o.QuestionNo
                  LEFT JOIN dbo.Subject s
                         ON s.PaperCode = o.PaperCode AND s.ExamCode = @ExamCode
                  WHERE o.Regno = @RegNo
                  ORDER BY o.PaperCode, o.QuestionNo",
                new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = candidate.RegNo },
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = candidate.ExamCode });

            gvObjections.DataBind();
        }
    }
}
