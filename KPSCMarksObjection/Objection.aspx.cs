using KPSCMarksObjection.DLL;
using KPSCMarksObjection.Payments;
using KPSCMarksObjection.Security;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace KPSCMarksObjection
{
    /// <summary>
    /// Filing marks objections.
    ///
    /// The candidate picks a subject, writes remarks and may attach a PDF.
    /// There is no fee: Submit saves the objection to dbo.MarksObjection
    /// (Database\migrations\006_MarksObjection.sql) and it is final. One
    /// objection per subject.
    /// </summary>
    public partial class Objection : KPSCMarksObjection.Security.CandidatePage
    {
        private const string Select = "---SELECT---";
        // 1 MB as Windows shows it (1,024 KB), so a file listed as under 1 MB is accepted.
        private const int MaxPdfBytes = 1024 * 1024;
        private const int MaxRemarks = 700;
        private const int MaxFileName = 50;

        private readonly SqlHelper sh = new SqlHelper();
        private ObjectionSession.LoginDetails ssen;

        protected void Page_Load(object sender, EventArgs e)
        {
            // CandidatePage has already ended the request for anyone not signed in.
            ssen = (ObjectionSession.LoginDetails)Session[AuthSession.CandidateKey];

            if (!IsPostBack)
            {
                BindSubject();
                RefreshState();
            }
        }

        // ---------------------------------------------------------------
        // Page state
        // ---------------------------------------------------------------

        /// <summary>Shows the submitted objections; the form only while the window is open.</summary>
        private void RefreshState()
        {
            ObjectionWindow window = ObjectionRules.GetWindow(ssen.ExamCode);

            pnlEntry.Visible = window.IsOpen;

            lblNotice.Visible = !window.IsOpen;
            lblNotice.Text = window.IsOpen ? "" : HttpUtility.HtmlEncode(window.ClosedMessage);

            gvObjections.DataSource = LoadSubmitted();
            gvObjections.DataBind();
        }

        private DataTable LoadSubmitted()
        {
            return sh.ExecuteDataTable(
                @"SELECT m.PaperCode AS SubjectCode, s.SubjectDescription AS SubjectDesc, m.VersionCode,
                         m.Remarks, m.FileName, m.SubmittedDate
                  FROM dbo.MarksObjection m
                  LEFT JOIN dbo.Subject s
                         ON s.PaperCode = m.PaperCode AND s.ExamCode = m.ExamCode
                  WHERE m.Regno = @RegNo AND m.ExamCode = @ExamCode
                  ORDER BY m.PaperCode",
                RegNoParam(),
                ExamParam());
        }

        // ---------------------------------------------------------------
        // Submit
        // ---------------------------------------------------------------

        protected void btnAdd_Click(object sender, EventArgs e)
        {
            // Re-checked here because the page may have been open for a while.
            if (!ObjectionRules.GetWindow(ssen.ExamCode).IsOpen)
            {
                Alert("The objection window is not open.");
                RefreshState();
                return;
            }

            // --- What the candidate entered ---

            if (ddlSubjectCode.SelectedIndex <= 0)
            {
                Alert("Please select Subject.");
                return;
            }

            string remarks = txtRemarks.Text.Trim();

            if (remarks.Length == 0)
            {
                Alert("Please enter remarks");
                return;
            }

            if (remarks.Length > MaxRemarks)
            {
                Alert("Remarks cannot exceed 700 characters.");
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

            string paperCode = ddlSubjectCode.SelectedValue;

            if (!PaperBelongsToExam(paperCode))
            {
                Alert("Please select Subject.");
                return;
            }

            if (AlreadySubmitted(paperCode))
            {
                Alert("An objection for this subject has already been submitted.");
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
                Alert("An objection for this subject has already been submitted.");
                RefreshState();
                return;
            }

            ClearEntryFields();
            RefreshState();
            Alert("Objection submitted successfully.");
        }

        private void ClearEntryFields()
        {
            ddlSubjectCode.SelectedIndex = 0;
            ddlVersionCode.Items.Clear();
            txtRemarks.Text = "";
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
        // Dropdowns
        // ---------------------------------------------------------------

        private void BindSubject()
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT PaperCode, SubjectDescription FROM dbo.Subject WHERE ExamCode = @ExamCode ORDER BY SubjectCode",
                ExamParam());

            ddlSubjectCode.Items.Clear();
            ddlSubjectCode.DataSource = dt;
            ddlSubjectCode.DataValueField = "PaperCode";
            ddlSubjectCode.DataTextField = "SubjectDescription";
            ddlSubjectCode.DataBind();
            ddlSubjectCode.Items.Insert(0, Select);
        }

        protected void ddlSubjectCode_SelectedIndexChanged(object sender, EventArgs e)
        {
            ddlVersionCode.Items.Clear();

            if (ddlSubjectCode.SelectedIndex <= 0)
                return;

            // Shown for information; the saved value is looked up again on Submit.
            string version = GetCandidateVersion(ddlSubjectCode.SelectedValue);

            if (!string.IsNullOrEmpty(version))
                ddlVersionCode.Items.Add(new ListItem(version, version));
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
