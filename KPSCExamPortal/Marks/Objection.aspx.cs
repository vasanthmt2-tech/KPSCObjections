using KPSCExamPortal.DLL;
using KPSCExamPortal.Payments;
using KPSCExamPortal.Security;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace KPSCExamPortal.Marks
{
    /// <summary>
    /// Marks objections: filing them and seeing the ones filed, on one page.
    ///
    /// The page shows one card per paper of the examination. A paper with an
    /// objection shows what was filed; a paper without one shows a form
    /// (remarks and an optional PDF) while the window is open. There is no
    /// fee: Submit saves the objection to dbo.MarksObjection
    /// (Database\migrations\006_MarksObjection.sql) and it is final. One
    /// objection per paper.
    /// </summary>
    public partial class Objection : KPSCExamPortal.Security.CandidatePage
    {
        // 1 MB as Windows shows it (1,024 KB), so a file listed as under 1 MB is accepted.
        private const int MaxPdfBytes = 1024 * 1024;
        private const int MaxRemarks = 2000;
        private const int MaxFileName = 50;

        private readonly SqlHelper sh = new SqlHelper();
        private ObjectionSession.LoginDetails ssen;

        protected void Page_Load(object sender, EventArgs e)
        {
            // CandidatePage has already ended the request for anyone not signed in.
            ssen = (ObjectionSession.LoginDetails)Session[AuthSession.CandidateKey];

            if (!IsPostBack)
            {
                RefreshState();
            }
        }

        // ---------------------------------------------------------------
        // Page state
        // ---------------------------------------------------------------

        /// <summary>Whether the objection window is open; the cards show a form only then.</summary>
        protected bool WindowOpen { get; private set; }

        /// <summary>Shows one card per paper: the objection filed, or the form while the window is open.</summary>
        private void RefreshState()
        {
            ObjectionWindow window = ObjectionRules.GetWindow(ssen.ExamCode);

            WindowOpen = window.IsOpen;

            lblNotice.Visible = !window.IsOpen;
            lblNotice.Text = window.IsOpen ? "" : HttpUtility.HtmlEncode(window.ClosedMessage);

            DataTable papers = LoadPapers();

            lblNoPapers.Visible = papers.Rows.Count == 0;

            rptPapers.DataSource = papers;
            rptPapers.DataBind();
        }

        /// <summary>
        /// Every paper of the examination with the candidate's objection for it,
        /// if any. VersionCode is the one saved with the objection, or else the
        /// candidate's own version for that paper.
        /// </summary>
        private DataTable LoadPapers()
        {
            return sh.ExecuteDataTable(
                @"SELECT s.PaperCode, s.SubjectDescription,
                         CASE WHEN m.PaperCode IS NOT NULL THEN m.VersionCode
                              WHEN s.PaperCode = 'P1' THEN NULLIF(LTRIM(RTRIM(c.P1VersionCode)), '')
                              ELSE NULLIF(LTRIM(RTRIM(c.P2VersionCode)), '') END AS VersionCode,
                         CAST(CASE WHEN m.PaperCode IS NULL THEN 0 ELSE 1 END AS bit) AS Filed,
                         m.Remarks, m.FileName, m.SubmittedDate
                  FROM dbo.Subject s
                  LEFT JOIN dbo.MarksObjection m
                         ON m.PaperCode = s.PaperCode AND m.ExamCode = s.ExamCode AND m.Regno = @RegNo
                  LEFT JOIN dbo.MarksCandidate c
                         ON c.RegNo = @RegNo
                  WHERE s.ExamCode = @ExamCode
                  ORDER BY s.SubjectCode",
                RegNoParam(),
                ExamParam());
        }

        // Card helpers used by the markup.

        protected bool IsFiled(object dataItem)
        {
            return Convert.ToBoolean(((DataRowView)dataItem)["Filed"]);
        }

        protected string StatusText(object dataItem)
        {
            return IsFiled(dataItem) ? "&#10004; Submitted" : "Not filed";
        }

        protected string StatusCss(object dataItem)
        {
            if (IsFiled(dataItem))
                return "status-filed";

            return WindowOpen ? "status-open" : "status-closed";
        }

        // ---------------------------------------------------------------
        // Submit
        // ---------------------------------------------------------------

        protected void rptPapers_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName != "Submit")
                return;

            // Re-checked here because the page may have been open for a while.
            if (!ObjectionRules.GetWindow(ssen.ExamCode).IsOpen)
            {
                Alert("The objection window is not open.");
                RefreshState();
                return;
            }

            TextBox txtRemarks = (TextBox)e.Item.FindControl("txtRemarks");
            FileUpload fileDocs = (FileUpload)e.Item.FindControl("fileDocs");

            if (txtRemarks == null || fileDocs == null)
            {
                RefreshState();
                return;
            }

            // --- What the candidate entered ---

            string remarks = txtRemarks.Text.Trim();

            if (remarks.Length == 0)
            {
                Alert("Please enter remarks");
                return;
            }

            if (remarks.Length > MaxRemarks)
            {
                Alert("Remarks cannot exceed " + MaxRemarks + " characters.");
                return;
            }

            // The supporting document is optional; when given it must be a PDF.
            byte[] fileData = null;

            if (fileDocs.HasFile)
            {
                fileData = fileDocs.FileBytes;

                if (fileData.Length > MaxPdfBytes)
                {
                    Alert("The supporting document must be 1 MB or smaller.");
                    return;
                }

                if (!IsPdf(fileDocs.FileName, fileData))
                {
                    Alert("Upload only PDF file.");
                    return;
                }
            }

            // --- What the server knows; nothing below trusts the form ---

            string paperCode = Convert.ToString(e.CommandArgument);

            if (!PaperBelongsToExam(paperCode))
            {
                Alert("This paper is not part of your examination.");
                RefreshState();
                return;
            }

            if (AlreadySubmitted(paperCode))
            {
                Alert("An objection for this paper has already been submitted.");
                RefreshState();
                return;
            }

            string versionCode = GetCandidateVersion(paperCode);

            // --- Print the objection's details on every page of the document ---

            string fileName = null;

            if (fileData != null)
            {
                try
                {
                    // Rebuild the file without scripts, actions, attachments or
                    // forms, so nobody who opens it later can be attacked by it.
                    PdfSanitizer.Result clean = PdfSanitizer.Sanitize(fileData);

                    if (clean.Removed.Count > 0)
                    {
                        AppLog.Write("WARN", "Active content removed from supporting PDF uploaded by " +
                                     ssen.RegNo + " from " + Request.UserHostAddress + ": " +
                                     string.Join(", ", clean.Removed));
                    }

                    fileData = PdfHeaderStamper.Stamp(clean.Pdf, DocumentHeader(paperCode, versionCode));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Supporting PDF rejected for " + ssen.RegNo + ": " + ex.Message);
                    Alert("This PDF could not be processed. Please upload a PDF of up to " +
                          PdfSanitizer.DefaultMaxPages + " pages that is not password-protected, " +
                          "or submit the objection without a document.");
                    return;
                }

                fileName = SafeFileName(fileDocs.FileName);
            }

            // --- Save ---

            try
            {
                sh.ExecuteNonQuery(
                    @"INSERT INTO dbo.MarksObjection
                          (Regno, ExamCode, PaperCode, VersionCode, Remarks, FileName, Document, IPAddress)
                      VALUES (@RegNo, @ExamCode, @PaperCode, @VersionCode, @Remarks, @FileName, @Document, @IPAddress)",
                    RegNoParam(),
                    ExamParam(),
                    new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paperCode },
                    new SqlParameter("@VersionCode", SqlDbType.Char, 1) { Value = (object)versionCode ?? DBNull.Value },
                    new SqlParameter("@Remarks", SqlDbType.NVarChar, MaxRemarks) { Value = remarks },
                    new SqlParameter("@FileName", SqlDbType.VarChar, MaxFileName) { Value = (object)fileName ?? DBNull.Value },
                    new SqlParameter("@Document", SqlDbType.VarBinary, -1) { Value = (object)fileData ?? DBNull.Value },
                    new SqlParameter("@IPAddress", SqlDbType.VarChar, 45) { Value = Request.UserHostAddress ?? "" });
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                // Submitted twice at the same moment (double click, two tabs).
                Alert("An objection for this paper has already been submitted.");
                RefreshState();
                return;
            }

            RefreshState();
            Alert("Objection submitted successfully.");
        }

        /// <summary>The line printed at the top of every page of a supporting document.</summary>
        private string DocumentHeader(string paperCode, string versionCode)
        {
            return "Reg No: " + ssen.RegNo +
                   "  |  Paper: " + paperCode +
                   "  |  Version: " + (versionCode ?? "-") +
                   "  |  Marks Objection";
        }

        /// <summary>A .pdf file whose content starts with the PDF signature.</summary>
        private static bool IsPdf(string fileName, byte[] data)
        {
            if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                return false;

            // The signature may be preceded by a little junk, so search the first 1 KB.
            string head = Encoding.ASCII.GetString(data, 0, Math.Min(data.Length, 1024));

            return head.Contains("%PDF-");
        }

        /// <summary>File name limited to safe characters and to the column length.</summary>
        private static string SafeFileName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName) ?? "";

            name = Regex.Replace(name, @"[^A-Za-z0-9 _\-\.]", "_").Trim();

            if (name.Length == 0)
                name = "document";

            const string extension = ".pdf";

            if (name.Length > MaxFileName - extension.Length)
                name = name.Substring(0, MaxFileName - extension.Length);

            return name + extension;
        }

        // ---------------------------------------------------------------
        // Lookups
        // ---------------------------------------------------------------

        private bool PaperBelongsToExam(string paperCode)
        {
            return Convert.ToInt32(sh.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.Subject WHERE ExamCode = @ExamCode AND PaperCode = @PaperCode",
                ExamParam(),
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paperCode })) > 0;
        }

        private bool AlreadySubmitted(string paperCode)
        {
            return Convert.ToInt32(sh.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.MarksObjection WHERE Regno = @RegNo AND ExamCode = @ExamCode AND PaperCode = @PaperCode",
                RegNoParam(),
                ExamParam(),
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paperCode })) > 0;
        }

        /// <summary>The question paper version this candidate wrote for the paper, or null.</summary>
        private string GetCandidateVersion(string paperCode)
        {
            object version = sh.ExecuteScalar(
                "SELECT CASE WHEN @PaperCode = 'P1' THEN P1VersionCode ELSE P2VersionCode END " +
                "FROM dbo.MarksCandidate WHERE RegNo = @RegNo",
                RegNoParam(),
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paperCode });

            string value = version == null || version == DBNull.Value
                ? ""
                : Convert.ToString(version).Trim().ToUpperInvariant();

            return value.Length == 1 ? value : null;
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private SqlParameter RegNoParam()
        {
            return new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = ssen.RegNo };
        }

        private SqlParameter ExamParam()
        {
            return new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = ssen.ExamCode };
        }

        private void Alert(string message)
        {
            ScriptManager.RegisterStartupScript(
                this, GetType(), Guid.NewGuid().ToString("N"),
                "alert('" + HttpUtility.JavaScriptStringEncode(message) + "');", true);
        }
    }
}
