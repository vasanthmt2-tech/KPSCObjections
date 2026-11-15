using System;
using KPSCObjections.Security;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI.WebControls;
using ClosedXML.Excel;
using KPSCObjections.DLL;

namespace KPSCObjections
{
    /// <summary>
    /// Admin page for loading candidates from an Excel file into dbo.Candidate,
    /// one examination at a time, and for viewing and deleting them.
    ///
    /// The Excel columns are the Candidate table's own columns; the
    /// examination comes from the dropdown. No other table is used.
    ///
    /// An upload is all or nothing: if any row is invalid, or any register
    /// number already exists, nothing is saved. A candidate who has filed an
    /// objection or started a payment cannot be deleted.
    /// </summary>
    public partial class UploadCandidates : KPSCObjections.Security.AdminPage
    {
        private const string Select = "---SELECT---";
        private const int MaxFileBytes = 5 * 1024 * 1024;
        private const int MaxErrorsShown = 10;
        private const int ColumnCount = 7;

        // Candidate.ExamCode is char(1).
        private const int MaxExamCodeLength = 1;

        private static readonly Regex RegNoPattern = new Regex(@"^[A-Za-z0-9]{1,10}$");
        private static readonly Regex MobilePattern = new Regex(@"^\d{10}$");
        private static readonly Regex VersionPattern = new Regex(@"^[A-L]$");

        // Accepted spellings of each header, compared without spaces, dots or case.
        private static readonly string[][] Headers =
        {
            new[] { "APPLNO", "APPLICATIONNO", "APPLICATIONNUMBER" },
            new[] { "REGNO", "REGISTERNO", "REGISTERNUMBER", "REGISTRATIONNO", "REGISTRATIONNUMBER" },
            new[] { "NAME", "CANDIDATENAME" },
            new[] { "DOB", "DATEOFBIRTH" },
            new[] { "MOBILENO", "MOBILE", "MOBILENUMBER" },
            new[] { "P1VERSIONCODE", "P1VERSION" },
            new[] { "P2VERSIONCODE", "P2VERSION" }
        };

        // The Candidate table's columns, in the order expected in the file.
        private static readonly string[] HeaderNames =
        {
            "Applno", "RegNo", "Name", "Dob", "MobileNo", "P1VersionCode", "P2VersionCode"
        };

        // A candidate with an objection or a payment must be kept.
        private const string HasRecordsCondition =
            @"(EXISTS (SELECT 1 FROM dbo.Objections o WHERE o.Regno = c.RegNo)
            OR EXISTS (SELECT 1 FROM dbo.PaymentTransactions p WHERE p.RegNo = c.RegNo))";

        private readonly SqlHelper sh = new SqlHelper();

        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["Objection_ConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindExamination();
                BindCandidates();
            }
        }

        // ---------------------------------------------------------
        // EXAMINATION DROPDOWN AND CANDIDATE GRID
        // ---------------------------------------------------------

        private string SelectedExamCode
        {
            get { return ddlExamCode.SelectedIndex > 0 ? ddlExamCode.SelectedValue.Trim() : ""; }
        }

        private void BindExamination()
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT ExamCode, ExamDescription FROM dbo.Exam ORDER BY ExamCode");

            ddlExamCode.Items.Clear();
            ddlExamCode.DataSource = dt;
            ddlExamCode.DataValueField = "ExamCode";
            ddlExamCode.DataTextField = "ExamDescription";
            ddlExamCode.DataBind();
            ddlExamCode.Items.Insert(0, new ListItem(Select, Select));
        }

        protected void ddlExamCode_SelectedIndexChanged(object sender, EventArgs e)
        {
            lblMessage.Text = "";
            txtSearch.Text = "";
            gvCandidates.PageIndex = 0;
            BindCandidates();
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            lblMessage.Text = "";
            gvCandidates.PageIndex = 0;
            BindCandidates();
        }

        protected void gvCandidates_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvCandidates.PageIndex = e.NewPageIndex;
            BindCandidates();
        }

        /// <summary>Candidates of the selected examination, filtered by the search box.</summary>
        private void BindCandidates()
        {
            string examCode = SelectedExamCode;

            pnlCandidates.Visible = examCode.Length > 0;
            lblSelectExam.Visible = examCode.Length == 0;

            if (examCode.Length == 0)
                return;

            string search = txtSearch.Text.Trim();

            // The search text is matched literally: % _ [ are not wildcards.
            string like = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%")
                                      .Replace("_", "\\_").Replace("[", "\\[") + "%";

            DataTable dt = sh.ExecuteDataTable(
                @"SELECT Applno, RegNo, Name, Dob, MobileNo, P1VersionCode, P2VersionCode
                  FROM dbo.Candidate
                  WHERE LTRIM(RTRIM(ExamCode)) = @ExamCode
                    AND (@Search = ''
                         OR RegNo  LIKE @Like ESCAPE '\'
                         OR Applno LIKE @Like ESCAPE '\'
                         OR Name   LIKE @Like ESCAPE '\')
                  ORDER BY RegNo",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode },
                new SqlParameter("@Search", SqlDbType.VarChar, 50) { Value = search },
                new SqlParameter("@Like", SqlDbType.VarChar, 110) { Value = like });

            gvCandidates.DataSource = dt;
            gvCandidates.DataBind();

            lblCount.Text = dt.Rows.Count + (search.Length == 0
                ? " candidate(s) in this examination."
                : " candidate(s) match the search.");

            btnDeleteAll.Visible = search.Length == 0 && dt.Rows.Count > 0;
        }

        // ---------------------------------------------------------
        // UPLOAD
        // ---------------------------------------------------------

        protected void btnUpload_Click(object sender, EventArgs e)
        {
            lblMessage.Text = "";

            string examCode = SelectedExamCode;

            if (examCode.Length == 0)
            {
                ShowMessage("Please select Examination.", false);
                return;
            }

            if (examCode.Length > MaxExamCodeLength)
            {
                ShowMessage(
                    "Candidates cannot be uploaded for this examination: the Candidate table " +
                    "holds examination codes of one character only.", false);
                return;
            }

            if (!fuCandidates.HasFile)
            {
                ShowMessage("Please select an Excel file.", false);
                return;
            }

            if (Path.GetExtension(fuCandidates.FileName).ToLowerInvariant() != ".xlsx")
            {
                ShowMessage("Only .xlsx Excel files are allowed.", false);
                return;
            }

            byte[] excelBytes = fuCandidates.FileBytes;

            if (excelBytes.Length == 0)
            {
                ShowMessage("The selected Excel file is empty.", false);
                return;
            }

            if (excelBytes.Length > MaxFileBytes)
            {
                ShowMessage("Excel file size cannot exceed 5 MB.", false);
                return;
            }

            // 1. Read and validate every row before touching the database.

            DataTable candidates;
            List<string> errors = new List<string>();

            try
            {
                candidates = ReadCandidates(excelBytes, ExamHasSecondPaper(examCode), errors);
            }
            catch (Exception ex)
            {
                ShowMessage(ErrorReport.Handle(ex, "Candidate Excel could not be read",
                    "The Excel file could not be read. Please check that it is a valid .xlsx file."), false);
                return;
            }

            if (errors.Count > 0)
            {
                ShowErrors(errors);
                return;
            }

            if (candidates.Rows.Count == 0)
            {
                ShowMessage("No candidate records found in the Excel file.", false);
                return;
            }

            // 2. Save all rows in one transaction.

            try
            {
                SaveCandidates(examCode, candidates);
            }
            catch (DuplicateCandidatesException ex)
            {
                ShowMessage(ex.Message, false);
                return;
            }
            catch (Exception ex)
            {
                ShowMessage(ErrorReport.Handle(ex, "Candidate upload failed",
                    "Upload failed. Nothing was saved. Please try again."), false);
                return;
            }

            ShowMessage(candidates.Rows.Count + " candidates uploaded successfully.", true);

            txtSearch.Text = "";
            gvCandidates.PageIndex = 0;
            BindCandidates();
        }

        /// <summary>
        /// Reads the first worksheet into a table shaped like dbo.Candidate.
        /// Problems are added to <paramref name="errors"/>; reading continues
        /// so the admin sees several at once.
        /// </summary>
        private DataTable ReadCandidates(byte[] excelBytes, bool secondPaperRequired, List<string> errors)
        {
            DataTable table = new DataTable();

            foreach (string name in HeaderNames)
                table.Columns.Add(name, typeof(string));

            using (MemoryStream stream = new MemoryStream(excelBytes))
            using (XLWorkbook workbook = new XLWorkbook(stream))
            {
                IXLWorksheet sheet = workbook.Worksheet(1);

                for (int col = 1; col <= ColumnCount; col++)
                {
                    string actual = NormalizeHeader(sheet.Cell(1, col).GetFormattedString());

                    if (!Headers[col - 1].Contains(actual))
                    {
                        errors.Add("Invalid Excel header in column " + col +
                                   ". Expected: " + HeaderNames[col - 1]);
                    }
                }

                if (errors.Count > 0)
                    return table;

                IXLRow last = sheet.LastRowUsed();
                int lastRow = last == null ? 1 : last.RowNumber();

                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int row = 2; row <= lastRow; row++)
                {
                    string[] v = new string[ColumnCount];
                    bool empty = true;

                    for (int col = 1; col <= ColumnCount; col++)
                    {
                        // Dob is read separately because it may be a date cell.
                        v[col - 1] = col == 4 ? "" : CellText(sheet.Cell(row, col));

                        if (!sheet.Cell(row, col).IsEmpty())
                            empty = false;
                    }

                    if (empty)
                        continue;

                    string applNo = v[0], regNo = v[1], name = v[2], mobile = v[4];
                    string p1 = v[5].ToUpperInvariant(), p2 = v[6].ToUpperInvariant();
                    string dob;
                    int before = errors.Count;

                    if (applNo.Length == 0 || applNo.Length > 25)
                        errors.Add(RowError(row, "Applno is required and cannot exceed 25 characters."));

                    if (!RegNoPattern.IsMatch(regNo))
                        errors.Add(RowError(row, "RegNo must be 1 to 10 letters or digits."));
                    else if (!seen.Add(regNo))
                        errors.Add(RowError(row, "RegNo " + regNo + " appears more than once in the file."));

                    if (name.Length == 0 || name.Length > 200)
                        errors.Add(RowError(row, "Name is required and cannot exceed 200 characters."));

                    if (!TryReadDob(sheet.Cell(row, 4), out dob))
                        errors.Add(RowError(row, "Dob must be a date (for example 1995-08-23 or 23-08-1995)."));

                    if (!MobilePattern.IsMatch(mobile))
                        errors.Add(RowError(row, "MobileNo must be exactly 10 digits."));

                    if (!VersionPattern.IsMatch(p1))
                        errors.Add(RowError(row, "P1VersionCode must be one letter A to L."));

                    if (p2.Length == 0 ? secondPaperRequired : !VersionPattern.IsMatch(p2))
                        errors.Add(RowError(row, "P2VersionCode must be one letter A to L."));

                    if (errors.Count == before)
                    {
                        table.Rows.Add(applNo, regNo, name,
                            dob.Length == 0 ? (object)DBNull.Value : dob,
                            mobile, p1,
                            p2.Length == 0 ? (object)DBNull.Value : p2);
                    }
                }
            }

            return table;
        }

        /// <summary>
        /// Inserts the candidates into dbo.Candidate. Register numbers that
        /// already exist cancel the whole upload.
        /// </summary>
        private void SaveCandidates(string examCode, DataTable candidates)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        // A temporary staging table, gone when the connection
                        // closes: duplicates are found with one query however
                        // large the file is.
                        Execute(con, transaction,
                            @"CREATE TABLE #CandidateUpload
                              (
                                  Applno        varchar(25)  COLLATE DATABASE_DEFAULT NOT NULL,
                                  RegNo         varchar(10)  COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
                                  Name          varchar(200) COLLATE DATABASE_DEFAULT NOT NULL,
                                  Dob           varchar(10)  COLLATE DATABASE_DEFAULT NULL,
                                  MobileNo      varchar(10)  COLLATE DATABASE_DEFAULT NOT NULL,
                                  P1VersionCode char(1)      COLLATE DATABASE_DEFAULT NOT NULL,
                                  P2VersionCode char(1)      COLLATE DATABASE_DEFAULT NULL
                              )");

                        using (SqlBulkCopy bulk = new SqlBulkCopy(con, SqlBulkCopyOptions.Default, transaction))
                        {
                            bulk.DestinationTableName = "#CandidateUpload";
                            bulk.BulkCopyTimeout = 120;

                            foreach (DataColumn column in candidates.Columns)
                                bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);

                            bulk.WriteToServer(candidates);
                        }

                        List<string> duplicates = new List<string>();
                        int duplicateCount = 0;

                        using (SqlCommand cmd = new SqlCommand(
                            @"SELECT u.RegNo
                              FROM #CandidateUpload u
                              INNER JOIN dbo.Candidate c ON c.RegNo = u.RegNo
                              ORDER BY u.RegNo", con, transaction))
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                duplicateCount++;

                                if (duplicates.Count < MaxErrorsShown)
                                    duplicates.Add(reader.GetString(0));
                            }
                        }

                        if (duplicateCount > 0)
                        {
                            throw new DuplicateCandidatesException(
                                duplicateCount + " register number(s) in the file already exist: " +
                                string.Join(", ", duplicates) +
                                (duplicateCount > duplicates.Count ? ", ..." : "") +
                                ". Upload cancelled; nothing was saved.");
                        }

                        using (SqlCommand cmd = new SqlCommand(
                            @"INSERT INTO dbo.Candidate
                                  (Applno, RegNo, Name, Dob, MobileNo, P1VersionCode, P2VersionCode, ExamCode)
                              SELECT Applno, RegNo, Name, Dob, MobileNo, P1VersionCode, P2VersionCode, @ExamCode
                              FROM #CandidateUpload", con, transaction))
                        {
                            cmd.CommandTimeout = 120;
                            cmd.Parameters.Add("@ExamCode", SqlDbType.Char, 1).Value = examCode;
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // DELETE
        // ---------------------------------------------------------

        protected void gvCandidates_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "DeleteCandidate")
                return;

            string examCode = SelectedExamCode;
            string regNo = Convert.ToString(e.CommandArgument);

            if (examCode.Length == 0 || !RegNoPattern.IsMatch(regNo))
            {
                ShowMessage("Invalid candidate selection.", false);
                return;
            }

            try
            {
                // One statement batch: the checks and the delete see the same data.
                object result = sh.ExecuteScalar(
                    @"SET XACT_ABORT ON;
                      BEGIN TRAN;
                      DECLARE @Result varchar(10) = 'NOTFOUND';

                      IF EXISTS (SELECT 1 FROM dbo.Candidate c WITH (UPDLOCK)
                                 WHERE c.RegNo = @RegNo AND LTRIM(RTRIM(c.ExamCode)) = @ExamCode)
                      BEGIN
                          IF EXISTS (SELECT 1 FROM dbo.Candidate c
                                     WHERE c.RegNo = @RegNo AND " + HasRecordsCondition + @")
                              SET @Result = 'INUSE';
                          ELSE
                          BEGIN
                              DELETE FROM dbo.CandidateOTP WHERE RegNo = @RegNo;
                              DELETE FROM dbo.Candidate WHERE RegNo = @RegNo;
                              SET @Result = 'DELETED';
                          END
                      END

                      COMMIT;
                      SELECT @Result;",
                    new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = regNo },
                    new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });

                switch (Convert.ToString(result))
                {
                    case "DELETED":
                        ShowMessage("Candidate " + regNo + " deleted.", true);
                        break;

                    case "INUSE":
                        ShowMessage(
                            "Candidate " + regNo + " cannot be deleted: objections or payments " +
                            "exist for this candidate.", false);
                        break;

                    default:
                        ShowMessage("Candidate not found.", false);
                        break;
                }
            }
            catch (Exception ex)
            {
                ShowMessage(ErrorReport.Handle(ex, "Candidate delete failed",
                    "The candidate could not be deleted. Please try again."), false);
            }

            BindCandidates();
        }

        /// <summary>
        /// Deletes every candidate of the selected examination except those
        /// with objections or payments, for redoing a wrong upload.
        /// </summary>
        protected void btnDeleteAll_Click(object sender, EventArgs e)
        {
            string examCode = SelectedExamCode;

            if (examCode.Length == 0)
            {
                ShowMessage("Please select Examination.", false);
                return;
            }

            try
            {
                DataTable dt = sh.ExecuteDataTable(
                    @"SET XACT_ABORT ON;
                      BEGIN TRAN;

                      DELETE o
                      FROM dbo.CandidateOTP o
                      INNER JOIN dbo.Candidate c ON c.RegNo = o.RegNo
                      WHERE LTRIM(RTRIM(c.ExamCode)) = @ExamCode AND NOT " + HasRecordsCondition + @";

                      DELETE c
                      FROM dbo.Candidate c
                      WHERE LTRIM(RTRIM(c.ExamCode)) = @ExamCode AND NOT " + HasRecordsCondition + @";

                      DECLARE @Deleted int = @@ROWCOUNT;

                      COMMIT;

                      SELECT @Deleted AS Deleted,
                             (SELECT COUNT(*) FROM dbo.Candidate c
                              WHERE LTRIM(RTRIM(c.ExamCode)) = @ExamCode) AS Kept;",
                    new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });

                int deleted = Convert.ToInt32(dt.Rows[0]["Deleted"]);
                int kept = Convert.ToInt32(dt.Rows[0]["Kept"]);

                ShowMessage(
                    deleted + " candidates deleted." +
                    (kept > 0 ? " " + kept + " kept because they have objections or payments." : ""),
                    true);
            }
            catch (Exception ex)
            {
                ShowMessage(ErrorReport.Handle(ex, "Candidate delete-all failed",
                    "The candidates could not be deleted. Please try again."), false);
            }

            gvCandidates.PageIndex = 0;
            BindCandidates();
        }

        // ---------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------

        /// <summary>True when the examination has a paper P2, so its version code is required.</summary>
        private bool ExamHasSecondPaper(string examCode)
        {
            return Convert.ToInt32(sh.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.Subject WHERE ExamCode = @ExamCode AND PaperCode = 'P2'",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode })) > 0;
        }

        /// <summary>
        /// Cell value as text. Whole numbers typed as numbers (register and
        /// mobile numbers often are) come back as plain digits, never in
        /// scientific notation.
        /// </summary>
        private static string CellText(IXLCell cell)
        {
            if (cell.DataType == XLDataType.Number)
            {
                string formatted = cell.GetFormattedString().Trim();

                // Keeps leading zeros when the cell has a format such as 0000000000.
                if (Regex.IsMatch(formatted, @"^\d+$"))
                    return formatted;

                double number = cell.GetDouble();

                if (Math.Abs(number) < 1e15 && Math.Floor(number) == number)
                    return ((long)number).ToString(CultureInfo.InvariantCulture);
            }

            return cell.GetFormattedString().Trim();
        }

        /// <summary>
        /// Date of birth as yyyy-MM-dd, from a date cell or common text
        /// formats. An empty cell is allowed and gives an empty string.
        /// </summary>
        private static bool TryReadDob(IXLCell cell, out string dob)
        {
            dob = "";

            if (cell.IsEmpty())
                return true;

            DateTime date;

            if (cell.DataType == XLDataType.DateTime)
            {
                date = cell.GetDateTime();
            }
            else
            {
                string[] formats = { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d-M-yyyy", "d/M/yyyy" };

                if (!DateTime.TryParseExact(cell.GetFormattedString().Trim(), formats,
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return false;
                }
            }

            if (date.Year < 1900 || date > DateTime.Today)
                return false;

            dob = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        private static string NormalizeHeader(string value)
        {
            return Regex.Replace(value ?? "", @"[\s\._]", "").ToUpperInvariant();
        }

        private static string RowError(int row, string message)
        {
            return "Row " + row + ": " + message;
        }

        private static void Execute(SqlConnection con, SqlTransaction transaction, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void ShowErrors(List<string> errors)
        {
            string text = "Upload cancelled; nothing was saved. " + errors.Count + " problem(s) found:<br />" +
                string.Join("<br />", errors.Take(MaxErrorsShown).Select(HttpUtility.HtmlEncode));

            if (errors.Count > MaxErrorsShown)
                text += "<br />... and " + (errors.Count - MaxErrorsShown) + " more.";

            lblMessage.Text = text;
            lblMessage.ForeColor = Color.Red;
        }

        private void ShowMessage(string message, bool success)
        {
            lblMessage.Text = HttpUtility.HtmlEncode(message);
            lblMessage.ForeColor = success ? Color.Green : Color.Red;
        }

        /// <summary>Raised when the file contains register numbers already in the database.</summary>
        private class DuplicateCandidatesException : UserFacingException
        {
            public DuplicateCandidatesException(string message) : base(message)
            {
            }
        }
    }
}
