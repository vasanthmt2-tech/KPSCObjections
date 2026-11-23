using System;
using KPSCMarksObjection.Security;
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
using KPSCMarksObjection.DLL;

namespace KPSCMarksObjection
{
    /// <summary>
    /// Admin page for loading marks objection candidates into
    /// dbo.MarksCandidate (Database\migrations\007_MarksCandidate.sql), one
    /// examination at a time, and for viewing and deleting them. The answer
    /// key portal's dbo.Candidate is never touched.
    ///
    /// Two Excel files, in this order:
    ///  1. Candidate details: Application Number, Hall ticket No (the
    ///     registration number), Candidate Name, Date of Birth. Adds new
    ///     candidates; a registration number that already exists cancels the
    ///     upload.
    ///  2. Version codes: S.No, RegNo, Version, Barcode, Paper, one row per
    ///     candidate per paper. Sets each paper's version code and answer
    ///     sheet barcode of candidates already uploaded; uploading a paper
    ///     again replaces them.
    ///
    /// Both uploads are all or nothing: if any row is invalid, nothing is
    /// saved. A candidate who has filed a marks objection is kept by "Delete
    /// All". Single candidates cannot be deleted from the grid.
    /// </summary>
    public partial class UploadCandidates : KPSCMarksObjection.Security.AdminPage
    {
        private const string Select = "---SELECT---";
        private const int MaxFileBytes = 10 * 1024 * 1024;
        private const int MaxErrorsShown = 10;

        // A 10 MB file holds about 300,000 candidates. The page may run for
        // 10 minutes (Web.config executionTimeout for this page).
        private const int UploadSqlTimeoutSeconds = 300;

        // MarksCandidate.ExamCode is char(1).
        private const int MaxExamCodeLength = 1;

        private static readonly Regex RegNoPattern = new Regex(@"^[A-Za-z0-9]{1,10}$");
        private static readonly Regex BarcodePattern = new Regex(@"^[A-Za-z0-9]{1,20}$");

        // Accepted spellings of each header, compared without spaces, dots,
        // underscores or case.
        private static readonly string[][] CandidateHeaders =
        {
            new[] { "APPLICATIONNUMBER", "APPLICATIONNO", "APPLNO" },
            new[] { "HALLTICKETNO", "HALLTICKETNUMBER", "REGNO", "REGISTERNO", "REGISTERNUMBER", "REGISTRATIONNO", "REGISTRATIONNUMBER" },
            new[] { "CANDIDATENAME", "NAME" },
            new[] { "DATEOFBIRTH", "DOB" }
        };

        private static readonly string[] CandidateHeaderNames =
        {
            "Application Number", "Hall ticket No", "Candidate Name", "Date of Birth"
        };

        private static readonly string[][] VersionHeaders =
        {
            new[] { "SNO", "SLNO", "SERIALNO", "SERIALNUMBER" },
            new[] { "REGNO", "HALLTICKETNO", "HALLTICKETNUMBER", "REGISTERNO", "REGISTRATIONNO", "REGISTRATIONNUMBER" },
            new[] { "VERSION", "VERSIONCODE" },
            new[] { "BARCODE", "BARCODENO", "BARCODENUMBER" },
            new[] { "PAPER", "PAPERCODE" }
        };

        private static readonly string[] VersionHeaderNames =
        {
            "S.No", "RegNo", "Version", "Barcode", "Paper"
        };

        // A candidate with a marks objection must be kept.
        private const string HasRecordsCondition =
            @"EXISTS (SELECT 1 FROM dbo.MarksObjection m WHERE m.Regno = c.RegNo)";

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
            lblVersionMessage.Text = "";
            txtSearch.Text = "";
            gvCandidates.PageIndex = 0;
            BindCandidates();
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            lblMessage.Text = "";
            lblVersionMessage.Text = "";
            gvCandidates.PageIndex = 0;
            BindCandidates();
        }

        protected void gvCandidates_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvCandidates.PageIndex = e.NewPageIndex;
            BindCandidates();
        }

        private const string CandidateFilter = @"
                  FROM dbo.MarksCandidate
                  WHERE ExamCode = @ExamCode
                    AND (@Search = ''
                         OR RegNo  LIKE @Like ESCAPE '\'
                         OR Applno LIKE @Like ESCAPE '\'
                         OR Name   LIKE @Like ESCAPE '\')";

        /// <summary>
        /// Candidates of the selected examination, filtered by the search box.
        /// Only the 50 rows shown are read: an examination can have 300,000
        /// candidates (a 10 MB file), and reading them all made every page
        /// change take about 15 seconds.
        /// </summary>
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

            int total = Convert.ToInt32(sh.ExecuteScalar(
                "SELECT COUNT(*) " + CandidateFilter,
                FilterParams(examCode, search, like)));

            int pages = Math.Max(1, (total + gvCandidates.PageSize - 1) / gvCandidates.PageSize);

            if (gvCandidates.PageIndex >= pages)
                gvCandidates.PageIndex = pages - 1;

            int skip = gvCandidates.PageIndex * gvCandidates.PageSize;

            SqlParameter[] parameters = FilterParams(examCode, search, like).Concat(new[]
            {
                new SqlParameter("@Skip", SqlDbType.Int) { Value = skip },
                new SqlParameter("@Take", SqlDbType.Int) { Value = gvCandidates.PageSize }
            }).ToArray();

            DataTable dt = sh.ExecuteDataTable(
                @"SELECT ROW_NUMBER() OVER (ORDER BY RegNo) AS SlNo,
                         Applno, RegNo, Name, Dob, P1VersionCode, P1Barcode, P2VersionCode, P2Barcode" +
                CandidateFilter + @"
                  ORDER BY RegNo
                  OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
                parameters);

            gvCandidates.VirtualItemCount = total;
            gvCandidates.DataSource = dt;
            gvCandidates.DataBind();

            lblCount.Text = total.ToString("N0", CultureInfo.InvariantCulture) + (search.Length == 0
                ? " candidate(s) in this examination."
                : " candidate(s) match the search.");

            btnDeleteAll.Visible = search.Length == 0 && total > 0;
        }

        private static SqlParameter[] FilterParams(string examCode, string search, string like)
        {
            return new[]
            {
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode },
                new SqlParameter("@Search", SqlDbType.VarChar, 50) { Value = search },
                new SqlParameter("@Like", SqlDbType.VarChar, 110) { Value = like }
            };
        }

        // ---------------------------------------------------------
        // UPLOAD 1: CANDIDATE DETAILS
        // ---------------------------------------------------------

        protected void btnUpload_Click(object sender, EventArgs e)
        {
            lblMessage.Text = "";
            lblVersionMessage.Text = "";

            string examCode;
            byte[] excelBytes;

            if (!CheckUpload(fuCandidates, lblMessage, out examCode, out excelBytes))
                return;

            // 1. Read and validate every row before touching the database.

            DataTable candidates;
            List<string> errors = new List<string>();

            try
            {
                candidates = ReadCandidates(excelBytes, errors);
            }
            catch (Exception ex)
            {
                ShowMessage(lblMessage, ErrorReport.Handle(ex, "Candidate Excel could not be read",
                    "The Excel file could not be read. Please check that it is a valid .xlsx file."), false);
                return;
            }

            if (errors.Count > 0)
            {
                ShowErrors(lblMessage, errors);
                return;
            }

            if (candidates.Rows.Count == 0)
            {
                ShowMessage(lblMessage, "No candidate records found in the Excel file.", false);
                return;
            }

            // 2. Save all rows in one transaction.

            try
            {
                SaveCandidates(examCode, candidates);
            }
            catch (UploadRejectedException ex)
            {
                ShowMessage(lblMessage, ex.Message, false);
                return;
            }
            catch (Exception ex)
            {
                ShowMessage(lblMessage, ErrorReport.Handle(ex, "Candidate upload failed",
                    "Upload failed. Nothing was saved. Please try again."), false);
                return;
            }

            AdminAudit.Record(Context, AdminAudit.UploadCandidates,
                "Exam " + examCode + ": " + candidates.Rows.Count + " candidates from file " +
                Path.GetFileName(fuCandidates.FileName));

            ShowMessage(lblMessage, candidates.Rows.Count + " candidates uploaded successfully. " +
                "Now upload their version codes (step 2).", true);

            txtSearch.Text = "";
            gvCandidates.PageIndex = 0;
            BindCandidates();
        }

        /// <summary>
        /// Reads the candidate file into a table shaped like the staging table.
        /// Problems are added to <paramref name="errors"/>; reading continues
        /// so the admin sees several at once.
        /// </summary>
        private DataTable ReadCandidates(byte[] excelBytes, List<string> errors)
        {
            DataTable table = new DataTable();
            table.Columns.Add("Applno", typeof(string));
            table.Columns.Add("RegNo", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Dob", typeof(string));

            using (MemoryStream stream = new MemoryStream(excelBytes))
            using (XLWorkbook workbook = new XLWorkbook(stream))
            {
                IXLWorksheet sheet = workbook.Worksheet(1);

                if (!CheckHeaders(sheet, CandidateHeaders, CandidateHeaderNames, errors))
                    return table;

                IXLRow last = sheet.LastRowUsed();
                int lastRow = last == null ? 1 : last.RowNumber();

                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int row = 2; row <= lastRow; row++)
                {
                    if (RowIsEmpty(sheet, row, CandidateHeaders.Length))
                        continue;

                    string applNo = CellText(sheet.Cell(row, 1));
                    string regNo = CellText(sheet.Cell(row, 2));
                    string name = CellText(sheet.Cell(row, 3));
                    string dob;
                    int before = errors.Count;

                    if (applNo.Length == 0 || applNo.Length > 25)
                        errors.Add(RowError(row, "Application Number is required and cannot exceed 25 characters."));

                    if (!RegNoPattern.IsMatch(regNo))
                        errors.Add(RowError(row, "Hall ticket No must be 1 to 10 letters or digits."));
                    else if (!seen.Add(regNo))
                        errors.Add(RowError(row, "Hall ticket No " + regNo + " appears more than once in the file."));

                    if (name.Length == 0 || name.Length > 200)
                        errors.Add(RowError(row, "Candidate Name is required and cannot exceed 200 characters."));

                    if (!TryReadDob(sheet.Cell(row, 4), out dob))
                        errors.Add(RowError(row, "Date of Birth is required (candidates log in with it) and must be a date, for example 20-10-2001."));

                    if (errors.Count == before)
                        table.Rows.Add(applNo, regNo, name, dob);
                }
            }

            return table;
        }

        /// <summary>
        /// Inserts the candidates into dbo.MarksCandidate. Registration
        /// numbers that already exist cancel the whole upload.
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
                                  Applno varchar(25)  COLLATE DATABASE_DEFAULT NOT NULL,
                                  RegNo  varchar(10)  COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
                                  Name   varchar(200) COLLATE DATABASE_DEFAULT NOT NULL,
                                  Dob    varchar(10)  COLLATE DATABASE_DEFAULT NOT NULL
                              )");

                        BulkCopy(con, transaction, "#CandidateUpload", candidates);

                        RejectIfAny(con, transaction,
                            @"SELECT u.RegNo
                              FROM #CandidateUpload u
                              INNER JOIN dbo.MarksCandidate c ON c.RegNo = u.RegNo
                              ORDER BY u.RegNo",
                            null,
                            " hall ticket number(s) in the file already exist: ");

                        using (SqlCommand cmd = new SqlCommand(
                            @"INSERT INTO dbo.MarksCandidate (Applno, RegNo, Name, Dob, ExamCode)
                              SELECT Applno, RegNo, Name, Dob, @ExamCode
                              FROM #CandidateUpload", con, transaction))
                        {
                            cmd.CommandTimeout = UploadSqlTimeoutSeconds;
                            cmd.Parameters.Add("@ExamCode", SqlDbType.Char, 1).Value = examCode;
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        RollBack(transaction);
                        throw;
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // UPLOAD 2: VERSION CODES AND BARCODES
        // ---------------------------------------------------------

        protected void btnUploadVersions_Click(object sender, EventArgs e)
        {
            lblMessage.Text = "";
            lblVersionMessage.Text = "";

            string examCode;
            byte[] excelBytes;

            if (!CheckUpload(fuVersions, lblVersionMessage, out examCode, out excelBytes))
                return;

            DataTable versions;
            List<string> errors = new List<string>();

            try
            {
                versions = ReadVersions(excelBytes, ExamPapers(examCode), errors);
            }
            catch (Exception ex)
            {
                ShowMessage(lblVersionMessage, ErrorReport.Handle(ex, "Version code Excel could not be read",
                    "The Excel file could not be read. Please check that it is a valid .xlsx file."), false);
                return;
            }

            if (errors.Count > 0)
            {
                ShowErrors(lblVersionMessage, errors);
                return;
            }

            if (versions.Rows.Count == 0)
            {
                ShowMessage(lblVersionMessage, "No version code records found in the Excel file.", false);
                return;
            }

            try
            {
                SaveVersions(examCode, versions);
            }
            catch (UploadRejectedException ex)
            {
                ShowMessage(lblVersionMessage, ex.Message, false);
                return;
            }
            catch (Exception ex)
            {
                ShowMessage(lblVersionMessage, ErrorReport.Handle(ex, "Version code upload failed",
                    "Upload failed. Nothing was saved. Please try again."), false);
                return;
            }

            int candidates = versions.AsEnumerable().Select(r => r.Field<string>("RegNo")).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            AdminAudit.Record(Context, AdminAudit.UploadVersionCodes,
                "Exam " + examCode + ": " + versions.Rows.Count + " version codes for " + candidates +
                " candidates from file " + Path.GetFileName(fuVersions.FileName));

            ShowMessage(lblVersionMessage, versions.Rows.Count + " version codes saved for " + candidates +
                " candidate(s).", true);

            txtSearch.Text = "";
            gvCandidates.PageIndex = 0;
            BindCandidates();
        }

        /// <summary>
        /// Reads the version code file. Each row is one candidate's paper:
        /// the version must be one of that paper's set letters (Security\KeySets.cs)
        /// and the paper one of the examination's papers.
        /// </summary>
        private DataTable ReadVersions(byte[] excelBytes, HashSet<string> examPapers, List<string> errors)
        {
            DataTable table = new DataTable();
            table.Columns.Add("RegNo", typeof(string));
            table.Columns.Add("Paper", typeof(string));
            table.Columns.Add("Version", typeof(string));
            table.Columns.Add("Barcode", typeof(string));

            using (MemoryStream stream = new MemoryStream(excelBytes))
            using (XLWorkbook workbook = new XLWorkbook(stream))
            {
                IXLWorksheet sheet = workbook.Worksheet(1);

                if (!CheckHeaders(sheet, VersionHeaders, VersionHeaderNames, errors))
                    return table;

                IXLRow last = sheet.LastRowUsed();
                int lastRow = last == null ? 1 : last.RowNumber();

                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int row = 2; row <= lastRow; row++)
                {
                    if (RowIsEmpty(sheet, row, VersionHeaders.Length))
                        continue;

                    string regNo = CellText(sheet.Cell(row, 2));
                    string version = CellText(sheet.Cell(row, 3)).ToUpperInvariant();
                    string barcode = CellText(sheet.Cell(row, 4));
                    string paper = CellText(sheet.Cell(row, 5)).ToUpperInvariant();
                    int before = errors.Count;

                    if (!RegNoPattern.IsMatch(regNo))
                        errors.Add(RowError(row, "RegNo must be 1 to 10 letters or digits."));

                    if (paper != "P1" && paper != "P2")
                        errors.Add(RowError(row, "Paper must be P1 or P2."));
                    else if (!examPapers.Contains(paper))
                        errors.Add(RowError(row, "This examination has no paper " + paper + "."));
                    else if (!KeySets.IsValid(paper, version))
                        errors.Add(RowError(row, "Version for " + paper + " must be one letter " + KeySets.Range(paper) + "."));

                    if (!BarcodePattern.IsMatch(barcode))
                        errors.Add(RowError(row, "Barcode is required and must be up to 20 letters or digits."));

                    if (errors.Count == before && !seen.Add(regNo + "|" + paper))
                        errors.Add(RowError(row, "RegNo " + regNo + " appears more than once for paper " + paper + "."));

                    if (errors.Count == before)
                        table.Rows.Add(regNo, paper, version, barcode);
                }
            }

            return table;
        }

        /// <summary>
        /// Sets the version code and barcode of each candidate's paper.
        /// Registration numbers not uploaded for this examination cancel the
        /// whole upload.
        /// </summary>
        private void SaveVersions(string examCode, DataTable versions)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        Execute(con, transaction,
                            @"CREATE TABLE #VersionUpload
                              (
                                  RegNo   varchar(10) COLLATE DATABASE_DEFAULT NOT NULL,
                                  Paper   char(2)     COLLATE DATABASE_DEFAULT NOT NULL,
                                  Version char(1)     COLLATE DATABASE_DEFAULT NOT NULL,
                                  Barcode varchar(20) COLLATE DATABASE_DEFAULT NOT NULL,
                                  PRIMARY KEY (RegNo, Paper)
                              )");

                        BulkCopy(con, transaction, "#VersionUpload", versions);

                        RejectIfAny(con, transaction,
                            // NOT EXISTS with a plain ExamCode comparison: the LEFT JOIN
                            // on LTRIM(RTRIM(ExamCode)) it replaces took 40 seconds for
                            // 88,000 rows against 147,000 candidates; this takes well
                            // under a second. ExamCode is char(1), written only by step 1.
                            @"SELECT DISTINCT u.RegNo
                              FROM #VersionUpload u
                              WHERE NOT EXISTS (SELECT 1 FROM dbo.MarksCandidate c
                                                WHERE c.RegNo = u.RegNo AND c.ExamCode = @ExamCode)
                              ORDER BY u.RegNo",
                            examCode,
                            " RegNo(s) in the file are not uploaded for this examination (upload them in step 1 first): ");

                        using (SqlCommand cmd = new SqlCommand(
                            @"UPDATE c SET P1VersionCode = u.Version, P1Barcode = u.Barcode
                              FROM dbo.MarksCandidate c
                              INNER JOIN #VersionUpload u ON u.RegNo = c.RegNo AND u.Paper = 'P1';

                              UPDATE c SET P2VersionCode = u.Version, P2Barcode = u.Barcode
                              FROM dbo.MarksCandidate c
                              INNER JOIN #VersionUpload u ON u.RegNo = c.RegNo AND u.Paper = 'P2';", con, transaction))
                        {
                            cmd.CommandTimeout = UploadSqlTimeoutSeconds;
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        RollBack(transaction);
                        throw;
                    }
                }
            }
        }

        /// <summary>The examination's paper codes (P1, P2) from dbo.Subject.</summary>
        private HashSet<string> ExamPapers(string examCode)
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT DISTINCT UPPER(LTRIM(RTRIM(PaperCode))) AS PaperCode FROM dbo.Subject WHERE ExamCode = @ExamCode",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });

            return new HashSet<string>(dt.AsEnumerable().Select(r => r.Field<string>("PaperCode")),
                                       StringComparer.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------
        // DELETE
        // ---------------------------------------------------------

        protected void btnDeleteAll_Click(object sender, EventArgs e)
        {
            lblVersionMessage.Text = "";
            string examCode = SelectedExamCode;

            if (examCode.Length == 0)
            {
                ShowMessage(lblMessage, "Please select Examination.", false);
                return;
            }

            try
            {
                DataTable dt = sh.ExecuteDataTable(
                    @"SET XACT_ABORT ON;
                      BEGIN TRAN;

                      DELETE c
                      FROM dbo.MarksCandidate c
                      WHERE c.ExamCode = @ExamCode AND NOT " + HasRecordsCondition + @";

                      DECLARE @Deleted int = @@ROWCOUNT;

                      COMMIT;

                      SELECT @Deleted AS Deleted,
                             (SELECT COUNT(*) FROM dbo.MarksCandidate c
                              WHERE c.ExamCode = @ExamCode) AS Kept;",
                    new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });

                int deleted = Convert.ToInt32(dt.Rows[0]["Deleted"]);
                int kept = Convert.ToInt32(dt.Rows[0]["Kept"]);

                AdminAudit.Record(Context, AdminAudit.DeleteAllCandidates,
                    "Exam " + examCode + ": " + deleted + " deleted, " + kept + " kept (have objections)");

                ShowMessage(lblMessage,
                    deleted + " candidates deleted." +
                    (kept > 0 ? " " + kept + " kept because they have submitted objections." : ""),
                    true);
            }
            catch (Exception ex)
            {
                ShowMessage(lblMessage, ErrorReport.Handle(ex, "Candidate delete-all failed",
                    "The candidates could not be deleted. Please try again."), false);
            }

            gvCandidates.PageIndex = 0;
            BindCandidates();
        }

        // ---------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------

        /// <summary>
        /// The checks both uploads share: an examination is selected and the
        /// file is a non-empty .xlsx of at most 10 MB (Web.config allows larger posts for this page).
        /// </summary>
        private bool CheckUpload(FileUpload upload, Label message, out string examCode, out byte[] excelBytes)
        {
            examCode = SelectedExamCode;
            excelBytes = null;

            if (examCode.Length == 0)
            {
                ShowMessage(message, "Please select Examination.", false);
                return false;
            }

            if (examCode.Length > MaxExamCodeLength)
            {
                ShowMessage(message,
                    "Candidates cannot be uploaded for this examination: the candidate table " +
                    "holds examination codes of one character only.", false);
                return false;
            }

            if (!upload.HasFile)
            {
                ShowMessage(message, "Please select an Excel file.", false);
                return false;
            }

            if (Path.GetExtension(upload.FileName).ToLowerInvariant() != ".xlsx")
            {
                ShowMessage(message, "Only .xlsx Excel files are allowed.", false);
                return false;
            }

            excelBytes = upload.FileBytes;

            if (excelBytes.Length == 0)
            {
                ShowMessage(message, "The selected Excel file is empty.", false);
                return false;
            }

            if (excelBytes.Length > MaxFileBytes)
            {
                ShowMessage(message, "Excel file size cannot exceed 10 MB.", false);
                return false;
            }

            return true;
        }

        /// <summary>Checks row 1 against the expected headers; adds an error per wrong column.</summary>
        private static bool CheckHeaders(IXLWorksheet sheet, string[][] headers, string[] names, List<string> errors)
        {
            for (int col = 1; col <= headers.Length; col++)
            {
                string actual = NormalizeHeader(sheet.Cell(1, col).GetFormattedString());

                if (!headers[col - 1].Contains(actual))
                    errors.Add("Invalid Excel header in column " + col + ". Expected: " + names[col - 1]);
            }

            return errors.Count == 0;
        }

        private static bool RowIsEmpty(IXLWorksheet sheet, int row, int columns)
        {
            for (int col = 1; col <= columns; col++)
            {
                if (CellText(sheet.Cell(row, col)).Length > 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Cancels the upload (by throwing) when the query returns any
        /// registration numbers; the message lists the first few.
        /// </summary>
        private static void RejectIfAny(SqlConnection con, SqlTransaction transaction, string sql,
                                        string examCode, string message)
        {
            List<string> found = new List<string>();
            int count = 0;

            using (SqlCommand cmd = new SqlCommand(sql, con, transaction))
            {
                // Not the 30-second default: the files can hold 300,000 rows.
                cmd.CommandTimeout = UploadSqlTimeoutSeconds;

                if (examCode != null)
                    cmd.Parameters.Add("@ExamCode", SqlDbType.VarChar, 5).Value = examCode;

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        count++;

                        if (found.Count < MaxErrorsShown)
                            found.Add(reader.GetString(0));
                    }
                }
            }

            if (count > 0)
            {
                throw new UploadRejectedException(
                    count + message + string.Join(", ", found) + (count > found.Count ? ", ..." : "") +
                    ". Upload cancelled; nothing was saved.");
            }
        }

        /// <summary>
        /// Rolls the upload back. When SQL Server has already ended the
        /// transaction (a command timeout does), Rollback throws; that must not
        /// hide the real error, which the caller rethrows and logs.
        /// </summary>
        private static void RollBack(SqlTransaction transaction)
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
                // Already rolled back.
            }
        }

        private static void BulkCopy(SqlConnection con, SqlTransaction transaction, string table, DataTable rows)
        {
            using (SqlBulkCopy bulk = new SqlBulkCopy(con, SqlBulkCopyOptions.Default, transaction))
            {
                bulk.DestinationTableName = table;
                bulk.BulkCopyTimeout = UploadSqlTimeoutSeconds;

                foreach (DataColumn column in rows.Columns)
                    bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);

                bulk.WriteToServer(rows);
            }
        }

        /// <summary>
        /// Cell value as text, without the stray quotes, tabs and spaces that
        /// exported files often carry (for example "&lt;tab&gt;GP2026..."). Whole
        /// numbers typed as numbers (hall ticket numbers often are) come back as
        /// plain digits, never in scientific notation.
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

            return Clean(cell.GetFormattedString());
        }

        private static string Clean(string value)
        {
            return (value ?? "").Replace(' ', ' ').Trim(' ', '\t', '\r', '\n', '"', '\'');
        }

        /// <summary>
        /// Date of birth as yyyy-MM-dd, from a date cell or common text
        /// formats. Required: candidates log in with it (Login.aspx.cs).
        /// </summary>
        private static bool TryReadDob(IXLCell cell, out string dob)
        {
            dob = "";

            if (cell.IsEmpty())
                return false;

            DateTime date;

            if (cell.DataType == XLDataType.DateTime)
            {
                date = cell.GetDateTime();
            }
            else
            {
                string[] formats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "yyyy-MM-dd" };

                if (!DateTime.TryParseExact(Clean(cell.GetFormattedString()), formats,
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
            return Regex.Replace(Clean(value), @"[\s\._]", "").ToUpperInvariant();
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

        private static void ShowErrors(Label label, List<string> errors)
        {
            string text = "Upload cancelled; nothing was saved. " + errors.Count + " problem(s) found:<br />" +
                string.Join("<br />", errors.Take(MaxErrorsShown).Select(HttpUtility.HtmlEncode));

            if (errors.Count > MaxErrorsShown)
                text += "<br />... and " + (errors.Count - MaxErrorsShown) + " more.";

            label.Text = text;
            label.ForeColor = Color.Red;
        }

        private static void ShowMessage(Label label, string message, bool success)
        {
            label.Text = HttpUtility.HtmlEncode(message);
            label.ForeColor = success ? Color.Green : Color.Red;
        }

        /// <summary>Raised when the file's rows clash with what is already saved.</summary>
        private class UploadRejectedException : UserFacingException
        {
            public UploadRejectedException(string message) : base(message)
            {
            }
        }
    }
}
