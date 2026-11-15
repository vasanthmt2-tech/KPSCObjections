using KPSCObjections.DLL;
using KPSCObjections.Payments;
using KPSCObjections.Security;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace KPSCObjections
{
    /// <summary>
    /// Filing objections.
    ///
    /// "Add" saves an objection and its PDF to the database as a draft
    /// (Objections.PaymentId IS NULL). "Pay Now" starts one payment for all
    /// drafts. While that payment is being verified nothing can be changed;
    /// if it fails the objections become drafts again.
    /// </summary>
    public partial class Objection : KPSCObjections.Security.CandidatePage
    {
        private const string Select = "---SELECT---";
        private const int MaxPdfBytes = 1024000;
        private const int MaxRemarks = 700;
        private const int MaxFileName = 50;
        private const string GraceAnswer = "5";
        private const string MultipleAnswersCategory = "2";
        private const string NoCorrectOptionCategory = "3";

        private readonly SqlHelper sh = new SqlHelper();
        private ObjectionSession.LoginDetails ssen;

        protected void Page_Load(object sender, EventArgs e)
        {
            // CandidatePage has already ended the request for anyone not signed in.
            ssen = (ObjectionSession.LoginDetails)Session[AuthSession.CandidateKey];

            if (!IsPostBack)
            {
                BindSubject();
                BindCategory();

                // The candidate may be coming back from the payment page.
                PaymentInfo pending = PaymentService.FindPending(ssen.RegNo);

                if (pending != null)
                    ShowPaymentOutcome(PaymentService.Verify(pending.OrderNo), false);

                RefreshState();
            }
        }

        // ---------------------------------------------------------------
        // Page state
        // ---------------------------------------------------------------

        /// <summary>Shows the drafts and enables only what is allowed right now.</summary>
        private void RefreshState()
        {
            ObjectionWindow window = ObjectionRules.GetWindow(ssen.ExamCode);
            PaymentInfo pending = PaymentService.FindPending(ssen.RegNo);
            DataTable drafts = LoadDrafts();

            bool editable = window.IsOpen && pending == null;

            pnlEntry.Visible = editable;

            lblNotice.Visible = !window.IsOpen;
            lblNotice.Text = window.IsOpen ? "" : HttpUtility.HtmlEncode(window.ClosedMessage);

            pnlPending.Visible = pending != null;

            if (pending != null)
            {
                lblPending.Text = HttpUtility.HtmlEncode(
                    "A payment of Rs. " + SbiEpay.FormatAmount(pending.Amount) +
                    " (reference " + pending.OrderNo + ") is being verified. " +
                    "If you have completed the payment, click Check Payment Status. " +
                    "Objections cannot be changed until this is resolved.");
            }

            gvObjections.DataSource = drafts;
            gvObjections.DataBind();

            // Last column holds the Delete button.
            gvObjections.Columns[gvObjections.Columns.Count - 1].Visible = editable;

            btnPayNow.Visible = editable && drafts.Rows.Count > 0;

            if (btnPayNow.Visible)
            {
                btnPayNow.Text = "Pay Now (Rs. " +
                    SbiEpay.FormatAmount(drafts.Rows.Count * ObjectionRules.Fee) + ")";
            }
        }

        /// <summary>Objections not yet paid for, or whose payment is still being verified.</summary>
        private DataTable LoadDrafts()
        {
            return sh.ExecuteDataTable(
                @"SELECT o.PaperCode AS SubjectCode, s.SubjectDescription AS SubjectDesc, o.VersionCode,
                         o.QuestionNo, o.KeyAnswer, o.ObjectionAnswer, o.Remarks, d.FileName, @Fee AS Fee
                  FROM dbo.Objections o
                  LEFT JOIN dbo.ObjectionsDocument d
                         ON d.Regno = o.Regno AND d.PaperCode = o.PaperCode AND d.QuestionNo = o.QuestionNo
                  LEFT JOIN dbo.Subject s
                         ON s.PaperCode = o.PaperCode AND s.ExamCode = @ExamCode
                  LEFT JOIN dbo.PaymentTransactions p
                         ON p.PaymentId = o.PaymentId
                  WHERE o.Regno = @RegNo AND (o.PaymentId IS NULL OR p.PaymentStatus = 'PENDING')
                  ORDER BY o.PaperCode, o.QuestionNo",
                RegNoParam(),
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = ssen.ExamCode },
                new SqlParameter("@Fee", SqlDbType.VarChar, 10) { Value = SbiEpay.FormatAmount(ObjectionRules.Fee) });
        }

        /// <summary>
        /// True when objections may be added, deleted or paid for. Re-checked on
        /// every action because the page may have been open for a while.
        /// </summary>
        private bool CanEdit()
        {
            if (!ObjectionRules.GetWindow(ssen.ExamCode).IsOpen)
            {
                Alert("The objection window is not open.");
                RefreshState();
                return false;
            }

            if (PaymentService.FindPending(ssen.RegNo) != null)
            {
                Alert("A payment is being verified. Objections cannot be changed until it is resolved.");
                RefreshState();
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------------
        // Add
        // ---------------------------------------------------------------

        protected void btnAdd_Click(object sender, EventArgs e)
        {
            if (!CanEdit())
                return;

            // --- What the candidate entered ---

            if (ddlSubjectCode.SelectedIndex <= 0)
            {
                Alert("Please select Subject.");
                return;
            }

            int questionNo;

            if (ddlQuestionNumber.SelectedIndex <= 0 ||
                !int.TryParse(ddlQuestionNumber.SelectedValue, out questionNo))
            {
                Alert("Please select Question Number.");
                return;
            }

            int categoryCode;

            if (ddlCategory.SelectedIndex <= 0 ||
                !int.TryParse(ddlCategory.SelectedValue, out categoryCode))
            {
                Alert("Please select Category.");
                return;
            }

            List<string> answers = cblObjectionAnswer.Items.Cast<ListItem>()
                .Where(item => item.Selected)
                .Select(item => item.Value)
                .ToList();

            if (answers.Count == 0)
            {
                Alert("Please select Objection Answer.");
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
                    Alert("Supportive document file size should be less than 1 MB");
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

            if (!CategoryIsActive(categoryCode))
            {
                Alert("Please select Category.");
                return;
            }

            string answerError = ValidateAnswers(categoryCode, answers);

            if (answerError != null)
            {
                Alert(answerError);
                return;
            }

            string versionCode = GetCandidateVersion(paperCode);

            if (string.IsNullOrEmpty(versionCode))
            {
                Alert("Your version code for this subject is not available. Please contact KPSC.");
                return;
            }

            string keyAnswer = GetKeyAnswer(paperCode, versionCode, questionNo);

            if (string.IsNullOrEmpty(keyAnswer))
            {
                Alert("The key answer for this question is not available.");
                return;
            }

            if (answers.Count == 1 && answers[0] == keyAnswer)
            {
                Alert("Your objection and Key answer are same");
                return;
            }

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

                    fileData = PdfHeaderStamper.Stamp(
                        clean.Pdf,
                        DocumentHeader(paperCode, questionNo, versionCode, keyAnswer, answers));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Supporting PDF rejected for " + ssen.RegNo + ": " + ex.Message);
                    Alert("This PDF could not be processed. Please upload a PDF of up to " +
                          PdfSanitizer.DefaultMaxPages + " pages that is not password-protected, " +
                          "or add the objection without a document.");
                    return;
                }

                fileName = SafeFileName(fileDocs.FileName);
            }

            // --- Save as a draft ---

            DataTable result = sh.ExecuteDataTable(
                "dbo.ObjectionsInsert",
                new SqlParameter("@Regno", SqlDbType.VarChar, 10) { Value = ssen.RegNo },
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paperCode },
                new SqlParameter("@VersionCode", SqlDbType.Char, 1) { Value = versionCode },
                new SqlParameter("@QuestionNo", SqlDbType.Int) { Value = questionNo },
                new SqlParameter("@KeyAnswer", SqlDbType.VarChar, 10) { Value = keyAnswer },
                new SqlParameter("@ObjectionAns", SqlDbType.VarChar, 10) { Value = string.Join(",", answers) },
                new SqlParameter("@CategoryCode", SqlDbType.Int) { Value = categoryCode },
                new SqlParameter("@Remarks", SqlDbType.NVarChar, MaxRemarks) { Value = remarks },
                new SqlParameter("@FileName", SqlDbType.VarChar, MaxFileName) { Value = (object)fileName ?? DBNull.Value },
                new SqlParameter("@ObjectionDocs", SqlDbType.VarBinary, -1) { Value = (object)fileData ?? DBNull.Value },
                new SqlParameter("@User", SqlDbType.VarChar, 50) { Value = ssen.RegNo },
                new SqlParameter("@IPAddress", SqlDbType.VarChar, 45) { Value = Request.UserHostAddress ?? "" });

            string outcome = result.Rows.Count > 0 ? Convert.ToString(result.Rows[0][0]) : "";

            if (outcome == "Data already exist.")
            {
                Alert("An objection for this question has already been added.");
                return;
            }

            if (outcome != "S")
            {
                System.Diagnostics.Trace.TraceError("ObjectionsInsert failed: " + outcome);
                Alert("The objection could not be saved. Please try again.");
                return;
            }

            ClearEntryFields();
            RefreshState();
            Alert("Objection added successfully.");
        }

        private void ClearEntryFields()
        {
            ddlSubjectCode.SelectedIndex = 0;
            ddlVersionCode.Items.Clear();
            ddlQuestionNumber.Items.Clear();
            ddlCategory.SelectedIndex = 0;
            txtKeyAnswer.Text = "";
            txtRemarks.Text = "";
            ConfigureObjectionAnswers();
        }

        /// <summary>
        /// Category 3 (no correct option): only Grace. Category 2 (more than one
        /// correct option): one or more. Every other category: exactly one.
        /// </summary>
        private static string ValidateAnswers(int categoryCode, List<string> answers)
        {
            string[] allowed = { "1", "2", "3", "4", GraceAnswer };

            if (answers.Any(a => !allowed.Contains(a)))
                return "Please select Objection Answer.";

            string category = categoryCode.ToString();

            if (category == NoCorrectOptionCategory)
            {
                return answers.Count == 1 && answers[0] == GraceAnswer
                    ? null
                    : "Only Grace can be selected for this category.";
            }

            if (category != MultipleAnswersCategory && answers.Count != 1)
                return "Only one Objection Answer can be selected for this category.";

            return null;
        }

        /// <summary>
        /// The line printed at the top of every page of a supporting document.
        /// Answer 5 is shown as "Grace", as it is on the form.
        /// </summary>
        private string DocumentHeader(
            string paperCode, int questionNo, string versionCode, string keyAnswer, List<string> answers)
        {
            string objectionAnswer = string.Join(", ",
                answers.Select(a => a == GraceAnswer ? "Grace" : a));

            return "Reg No: " + ssen.RegNo +
                   "  |  Paper: " + paperCode +
                   "  |  Q.No: " + questionNo +
                   "  |  Version: " + versionCode +
                   "  |  Key Answer: " + keyAnswer +
                   "  |  Objection Answer: " + objectionAnswer;
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
        // Delete a draft
        // ---------------------------------------------------------------

        protected void gvObjections_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            if (!CanEdit())
                return;

            DataKey key = gvObjections.DataKeys[e.RowIndex];

            // Only a draft (PaymentId IS NULL) of this candidate can be deleted.
            sh.ExecuteNonQuery(
                @"SET XACT_ABORT ON;
                  BEGIN TRAN;
                  DELETE d
                  FROM dbo.ObjectionsDocument d
                  INNER JOIN dbo.Objections o
                          ON o.Regno = d.Regno AND o.PaperCode = d.PaperCode AND o.QuestionNo = d.QuestionNo
                  WHERE o.Regno = @RegNo AND o.PaperCode = @PaperCode AND o.QuestionNo = @QuestionNo
                    AND o.PaymentId IS NULL;
                  DELETE FROM dbo.Objections
                  WHERE Regno = @RegNo AND PaperCode = @PaperCode AND QuestionNo = @QuestionNo
                    AND PaymentId IS NULL;
                  COMMIT;",
                RegNoParam(),
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = Convert.ToString(key.Values["SubjectCode"]) },
                new SqlParameter("@QuestionNo", SqlDbType.Int) { Value = Convert.ToInt32(key.Values["QuestionNo"]) });

            RefreshState();
        }

        // ---------------------------------------------------------------
        // Payment
        // ---------------------------------------------------------------

        protected void btnPayNow_Click(object sender, EventArgs e)
        {
            if (!CanEdit())
                return;

            string orderNo;
            decimal amount;

            switch (PaymentService.Start(ssen.RegNo, out orderNo, out amount))
            {
                case "OK":
                    // Pay.aspx posts this order to SBI ePay.
                    Session["PayOrderNo"] = orderNo;
                    Response.Redirect("~/Pay.aspx", true);
                    break;

                case "NO_DRAFTS":
                    Alert("Please add at least one objection.");
                    break;

                case "WINDOW_CLOSED":
                    Alert("The objection window is not open.");
                    break;

                default:
                    Alert("A payment is already being verified.");
                    break;
            }

            RefreshState();
        }

        protected void btnCheckPayment_Click(object sender, EventArgs e)
        {
            PaymentInfo pending = PaymentService.FindPending(ssen.RegNo);

            if (pending != null)
                ShowPaymentOutcome(PaymentService.Verify(pending.OrderNo), true);

            RefreshState();
        }

        private void ShowPaymentOutcome(string status, bool reportPending)
        {
            if (status == PaymentService.Success)
            {
                Alert("Payment completed successfully. Your objections are submitted and listed under My Objections.");
            }
            else if (status == PaymentService.Failed)
            {
                Alert("The payment was not completed. Your objections are saved; click Pay Now to pay again.");
            }
            else if (reportPending)
            {
                Alert("The payment is not confirmed yet. Please check again after some time.");
            }
        }

        // ---------------------------------------------------------------
        // Dropdowns
        // ---------------------------------------------------------------

        private void BindSubject()
        {
            BindDropDownList(ddlSubjectCode, sh.ExecuteDataTable(
                "SELECT PaperCode, SubjectDescription FROM dbo.Subject WHERE ExamCode = @ExamCode ORDER BY SubjectCode",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = ssen.ExamCode }));
        }

        private void BindCategory()
        {
            BindDropDownList(ddlCategory, sh.ExecuteDataTable(
                "SELECT CategoryCode, CategoryDescription FROM dbo.Categories WHERE IsActive = 'Y'"));
        }

        private static void BindDropDownList(ListControl list, DataTable dt)
        {
            list.Items.Clear();
            list.DataSource = dt;
            list.DataValueField = dt.Columns[0].ColumnName;
            list.DataTextField = dt.Columns[1].ColumnName;
            list.DataBind();
            list.Items.Insert(0, Select);
        }

        protected void ddlSubjectCode_SelectedIndexChanged(object sender, EventArgs e)
        {
            ddlVersionCode.Items.Clear();
            ddlQuestionNumber.Items.Clear();
            txtKeyAnswer.Text = "";

            if (ddlSubjectCode.SelectedIndex <= 0)
                return;

            BindDropDownList(ddlQuestionNumber, sh.ExecuteDataTable(
                "SELECT QuestionNo, QuestionNo AS QuestionText FROM dbo.QuestionNo ORDER BY QuestionNo"));

            // Shown for information; the saved value is looked up again on Add.
            string version = GetCandidateVersion(ddlSubjectCode.SelectedValue);

            if (!string.IsNullOrEmpty(version))
                ddlVersionCode.Items.Add(new ListItem(version, version));
        }

        protected void ddlQuestionNumber_SelectedIndexChanged(object sender, EventArgs e)
        {
            txtKeyAnswer.Text = "";

            int questionNo;

            if (ddlSubjectCode.SelectedIndex <= 0 || ddlQuestionNumber.SelectedIndex <= 0 ||
                !int.TryParse(ddlQuestionNumber.SelectedValue, out questionNo))
            {
                return;
            }

            string version = GetCandidateVersion(ddlSubjectCode.SelectedValue);

            if (!string.IsNullOrEmpty(version))
                txtKeyAnswer.Text = GetKeyAnswer(ddlSubjectCode.SelectedValue, version, questionNo) ?? "";
        }

        protected void ddlCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigureObjectionAnswers();
        }

        private void ConfigureObjectionAnswers()
        {
            cblObjectionAnswer.ClearSelection();

            bool graceOnly = ddlCategory.SelectedValue == NoCorrectOptionCategory;

            foreach (ListItem item in cblObjectionAnswer.Items)
                item.Enabled = !graceOnly || item.Value == GraceAnswer;
        }

        // ---------------------------------------------------------------
        // Lookups
        // ---------------------------------------------------------------

        private bool PaperBelongsToExam(string paperCode)
        {
            return Convert.ToInt32(sh.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.Subject WHERE ExamCode = @ExamCode AND PaperCode = @PaperCode",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = ssen.ExamCode },
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paperCode })) > 0;
        }

        private bool CategoryIsActive(int categoryCode)
        {
            return Convert.ToInt32(sh.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.Categories WHERE CategoryCode = @CategoryCode AND IsActive = 'Y'",
                new SqlParameter("@CategoryCode", SqlDbType.Int) { Value = categoryCode })) > 0;
        }

        /// <summary>The question paper version this candidate wrote for the paper.</summary>
        private string GetCandidateVersion(string paperCode)
        {
            object version = sh.ExecuteScalar(
                "SELECT CASE WHEN @PaperCode = 'P1' THEN P1VersionCode ELSE P2VersionCode END " +
                "FROM dbo.Candidate WHERE RegNo = @RegNo",
                RegNoParam(),
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paperCode });

            return version == null || version == DBNull.Value
                ? null
                : Convert.ToString(version).Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Key answer for a question as numbered in the candidate's version:
        /// finds the master question whose Set column for that version holds
        /// the candidate's question number.
        /// </summary>
        private string GetKeyAnswer(string paperCode, string versionCode, int questionNo)
        {
            object answer = sh.ExecuteScalar(
                @"SELECT TOP 1 KA.MasterAnswer
                  FROM dbo.KeyAnswers KA
                  WHERE KA.ExamCode = @ExamCode AND KA.SubjectCode = @PaperCode
                    AND CASE @VersionCode
                            WHEN 'A' THEN KA.SetA WHEN 'B' THEN KA.SetB WHEN 'C' THEN KA.SetC
                            WHEN 'D' THEN KA.SetD WHEN 'E' THEN KA.SetE WHEN 'F' THEN KA.SetF
                            WHEN 'G' THEN KA.SetG WHEN 'H' THEN KA.SetH WHEN 'I' THEN KA.SetI
                            WHEN 'J' THEN KA.SetJ WHEN 'K' THEN KA.SetK WHEN 'L' THEN KA.SetL
                        END = @QuestionNo",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = ssen.ExamCode },
                new SqlParameter("@PaperCode", SqlDbType.VarChar, 5) { Value = paperCode },
                new SqlParameter("@VersionCode", SqlDbType.VarChar, 1) { Value = versionCode },
                new SqlParameter("@QuestionNo", SqlDbType.VarChar, 20) { Value = questionNo.ToString() });

            return answer == null || answer == DBNull.Value
                ? null
                : Convert.ToString(answer).Trim();
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private SqlParameter RegNoParam()
        {
            return new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = ssen.RegNo };
        }

        private void Alert(string message)
        {
            ScriptManager.RegisterStartupScript(
                this, GetType(), Guid.NewGuid().ToString("N"),
                "alert('" + HttpUtility.JavaScriptStringEncode(message) + "');", true);
        }
    }
}
