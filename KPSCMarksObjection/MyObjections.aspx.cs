using KPSCMarksObjection.DLL;
using KPSCMarksObjection.Security;
using System;
using System.Data;
using System.Data.SqlClient;

namespace KPSCMarksObjection
{
    /// <summary>
    /// The marks objections the candidate has submitted for the examination
    /// they signed in to (dbo.MarksObjection). There is no payment, so every
    /// row is final.
    /// </summary>
    public partial class MyObjections : KPSCMarksObjection.Security.CandidatePage
    {
        private readonly SqlHelper sh = new SqlHelper();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack)
                return;

            ObjectionSession.LoginDetails candidate =
                (ObjectionSession.LoginDetails)Session[AuthSession.CandidateKey];

            gvObjections.DataSource = sh.ExecuteDataTable(
                @"SELECT m.PaperCode, s.SubjectDescription AS SubjectDesc, m.VersionCode,
                         m.Remarks, m.FileName, m.SubmittedDate
                  FROM dbo.MarksObjection m
                  LEFT JOIN dbo.Subject s
                         ON s.PaperCode = m.PaperCode AND s.ExamCode = m.ExamCode
                  WHERE m.Regno = @RegNo AND m.ExamCode = @ExamCode
                  ORDER BY m.PaperCode",
                new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = candidate.RegNo },
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = candidate.ExamCode });

            gvObjections.DataBind();
        }
    }
}
