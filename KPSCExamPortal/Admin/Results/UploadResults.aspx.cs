using System;
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
using KPSCExamPortal.DLL;
using KPSCExamPortal.Results;
using KPSCExamPortal.Security;

namespace KPSCExamPortal.Admin.Results
{
    /// <summary>
    /// Admin page for loading the OMR results into dbo.OmrResult
    /// (Database\migrations\008_OmrResult.sql), one examination at a time.
    ///
    /// The file has one row per candidate per paper: S.No, RegNo, Version,
    /// Barcode, Paper, Dist, Centre, Q1..Q100 (the marked answers), Cr., InCr.,
    /// NA., LeftBlank, MarksObt., Rank. It is read row by row (XlsxRowReader)
    /// and streamed into a staging table, so a 50 MB file does not have to be
    /// held in memory. A file is all or nothing: any invalid row cancels it.
    /// A candidate's paper already uploaded is replaced, so a large result
    /// can be uploaded in parts and corrections uploaded again.
    /// </summary>
    public partial class UploadResults : AdminPage
    {
        private const string Select = "---SELECT---";
        private const int MaxFileBytes = 50 * 1024 * 1024;
        private const int MaxErrorsShown = 10;
        private const int MaxErrorsCollected = 1000;
        private const int Questions = 100;
        private const int BatchRows = 5000;
        private const int SqlTimeoutSeconds = 600;
        private const int ColumnCount = 7 + Questions + 6;

        private static readonly Regex RegNoPattern = new Regex(@"^[A-Za-z0-9]{1,10}$");
        private static readonly Regex WholeNumber = new Regex(@"^\d+(\.0+)?$");

        private readonly SqlHelper sh = new SqlHelper();

        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["Objection_ConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindExamination();
                BindSummary();
            }
        }

        private string SelectedExamCode
        {
            get { return ddlExamCode.SelectedIndex > 0 ? ddlExamCode.SelectedValue.Trim() : ""; }
        }

        private void BindExamination()
        {
            DataTable dt = sh.ExecuteDataTable("SELECT ExamCode, ExamDescription FROM dbo.Exam ORDER BY ExamCode");

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
            BindSummary();
        }

        /// <summary>Results uploaded for the selected examination, per paper.</summary>
        private void BindSummary()
        {
            string examCode = SelectedExamCode;

            pnlSummary.Visible = examCode.Length > 0;
            lblSelectExam.Visible = examCode.Length == 0;

            if (examCode.Length == 0)
                return;

            // dbo.OmrResult has no examination column: a registration number belongs
            // to one examination, so this examination's results are the rows of its
            // candidates (uploads accept only those).
            gvSummary.DataSource = sh.ExecuteDataTable(
                @"SELECT r.Paper AS PaperCode,
                         ISNULL(MAX(s.SubjectDescription), r.Paper) AS SubjectDesc,
                         COUNT(*) AS Candidates
                  FROM dbo.OmrResult r
                  INNER JOIN dbo.MarksCandidate c ON c.RegNo = r.RegNo AND c.ExamCode = @ExamCode
                  LEFT JOIN dbo.Subject s ON s.ExamCode = @ExamCode AND s.PaperCode = r.Paper
                  GROUP BY r.Paper
                  ORDER BY r.Paper",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });
            gvSummary.DataBind();
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

            if (!fuResults.HasFile)
            {
                ShowMessage("Please select an Excel file.", false);
                return;
            }

            if (Path.GetExtension(fuResults.FileName).ToLowerInvariant() != ".xlsx")
            {
                ShowMessage("Only .xlsx Excel files are allowed.", false);
                return;
            }

            if (fuResults.PostedFile.ContentLength == 0)
            {
                ShowMessage("The selected Excel file is empty.", false);
                return;
            }

            if (fuResults.PostedFile.ContentLength > MaxFileBytes)
            {
                ShowMessage("Excel file size cannot exceed 50 MB. Split it (for example by district) and upload the parts.", false);
                return;
            }

            HashSet<string> examPapers = ExamPapers(examCode);
            List<string> errors = new List<string>();
            UploadCounts counts;

            try
            {
                counts = LoadAndSave(examCode, examPapers, errors);
            }
            catch (UserFacingException ex)
            {
                ShowMessage(ex.Message, false);
                return;
            }
            catch (Exception ex)
            {
                ShowMessage(ErrorReport.Handle(ex, "Result upload failed",
                    "Upload failed. Nothing was saved. Please check that it is a valid .xlsx file and try again."), false);
                return;
            }

            if (errors.Count > 0)
            {
                ShowErrors(errors);
                return;
            }

            if (counts.Rows == 0)
            {
                ShowMessage("No result rows found in the Excel file.", false);
                return;
            }

            AdminAudit.Record(Context, AdminAudit.UploadResults,
                "Exam " + examCode + ": " + counts.Rows + " results (" + counts.Added + " new, " + counts.Replaced +
                " replaced) from file " + Path.GetFileName(fuResults.FileName));

            ShowMessage(counts.Rows.ToString("N0", CultureInfo.InvariantCulture) + " results saved: " +
                counts.Added.ToString("N0", CultureInfo.InvariantCulture) + " new, " +
                counts.Replaced.ToString("N0", CultureInfo.InvariantCulture) + " replaced.", true);

            BindSummary();
        }

        private sealed class UploadCounts
        {
            public int Rows, Added, Replaced;
        }

        /// <summary>
        /// Reads, checks and stages every row, then merges them into
        /// dbo.OmrResult in one transaction. With any row error nothing is
        /// saved (the transaction is rolled back) and the errors are returned.
        /// </summary>
        private UploadCounts LoadAndSave(string examCode, HashSet<string> examPapers, List<string> errors)
        {
            UploadCounts counts = new UploadCounts();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        Execute(con, transaction,
                            @"CREATE TABLE #OmrUpload
                              (
                                  SNo        int NULL,
                                  RegNo      varchar(10) COLLATE DATABASE_DEFAULT NOT NULL,
                                  Version    char(1) COLLATE DATABASE_DEFAULT NOT NULL,
                                  Barcode    varchar(20) COLLATE DATABASE_DEFAULT NULL,
                                  Paper      varchar(2) COLLATE DATABASE_DEFAULT NOT NULL,
                                  Dist       nvarchar(100) COLLATE DATABASE_DEFAULT NULL,
                                  Centre     nvarchar(200) COLLATE DATABASE_DEFAULT NULL,
                                  Q1         varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q2         varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q3         varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q4         varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q5         varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q6         varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q7         varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q8         varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q9         varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q10        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q11        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q12        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q13        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q14        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q15        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q16        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q17        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q18        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q19        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q20        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q21        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q22        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q23        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q24        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q25        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q26        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q27        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q28        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q29        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q30        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q31        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q32        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q33        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q34        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q35        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q36        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q37        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q38        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q39        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q40        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q41        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q42        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q43        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q44        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q45        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q46        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q47        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q48        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q49        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q50        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q51        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q52        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q53        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q54        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q55        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q56        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q57        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q58        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q59        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q60        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q61        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q62        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q63        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q64        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q65        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q66        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q67        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q68        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q69        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q70        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q71        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q72        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q73        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q74        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q75        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q76        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q77        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q78        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q79        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q80        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q81        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q82        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q83        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q84        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q85        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q86        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q87        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q88        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q89        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q90        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q91        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q92        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q93        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q94        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q95        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q96        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q97        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q98        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q99        varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Q100       varchar(5) COLLATE DATABASE_DEFAULT NULL,
                                  Correct    int NOT NULL,
                                  Incorrect  int NOT NULL,
                                  NA         int NOT NULL,
                                  LeftBlank  int NOT NULL,
                                  MarksObt   decimal(7,2) NOT NULL,
                                  [Rank]     int NULL,
                                  PRIMARY KEY (RegNo, Paper)
                              )");

                        DataTable batch = NewBatch();
                        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        using (Stream stream = SeekableUpload())
                        using (XlsxRowReader reader = new XlsxRowReader(stream))
                        {
                            int rowNumber;
                            string[] cells;

                            if (!reader.Read(out rowNumber, out cells) || !CheckHeaders(cells, errors))
                            {
                                if (errors.Count == 0)
                                    errors.Add("The Excel file has no header row.");

                                RollBack(transaction);
                                return counts;
                            }

                            while (reader.Read(out rowNumber, out cells))
                            {
                                if (cells.All(c => Clean(c).Length == 0))
                                    continue;

                                DataRow row = ReadRow(batch, rowNumber, cells, examPapers, seen, errors);

                                if (row != null)
                                {
                                    batch.Rows.Add(row);
                                    counts.Rows++;
                                }

                                if (errors.Count >= MaxErrorsCollected)
                                    break;

                                if (batch.Rows.Count >= BatchRows)
                                {
                                    if (errors.Count == 0)
                                        BulkCopy(con, transaction, batch);

                                    batch.Clear();
                                }
                            }
                        }

                        if (errors.Count > 0)
                        {
                            RollBack(transaction);
                            return counts;
                        }

                        if (batch.Rows.Count > 0)
                            BulkCopy(con, transaction, batch);

                        // dbo.OmrResult has no examination column, so every row must
                        // belong to this examination's candidate list: otherwise a
                        // wrong file could replace another examination's results.
                        List<string> unknown = new List<string>();
                        int unknownCount = 0;

                        using (SqlCommand check = new SqlCommand(
                            @"SELECT u.RegNo, COUNT(*) OVER () AS Total
                              FROM #OmrUpload u
                              WHERE NOT EXISTS (SELECT 1 FROM dbo.MarksCandidate c
                                                WHERE c.RegNo = u.RegNo AND c.ExamCode = @ExamCode)
                              ORDER BY u.RegNo
                              OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY;",
                            con, transaction))
                        {
                            check.CommandTimeout = SqlTimeoutSeconds;
                            check.Parameters.Add("@ExamCode", SqlDbType.VarChar, 5).Value = examCode;

                            using (SqlDataReader dr = check.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    unknown.Add(dr.GetString(0));
                                    unknownCount = dr.GetInt32(1);
                                }
                            }
                        }

                        if (unknownCount > 0)
                        {
                            errors.Add(unknownCount.ToString("N0", CultureInfo.InvariantCulture) +
                                " row(s) have a registration number that is not in this examination's candidate list " +
                                "(upload the candidates in the marks portal first, or choose the right examination): " +
                                string.Join(", ", unknown) + (unknownCount > unknown.Count ? ", ..." : "") + ".");
                            RollBack(transaction);
                            return counts;
                        }

                        using (SqlCommand cmd = new SqlCommand(
                            @"SELECT
                                  (SELECT COUNT(*) FROM #OmrUpload u
                                   WHERE EXISTS (SELECT 1 FROM dbo.OmrResult r
                                                 WHERE r.RegNo = u.RegNo AND r.Paper = u.Paper));

                              UPDATE r SET SNo = u.SNo, Version = u.Version, Barcode = u.Barcode, Dist = u.Dist, Centre = u.Centre, Q1 = u.Q1, Q2 = u.Q2, Q3 = u.Q3, Q4 = u.Q4, Q5 = u.Q5, Q6 = u.Q6, Q7 = u.Q7, Q8 = u.Q8, Q9 = u.Q9, Q10 = u.Q10, Q11 = u.Q11, Q12 = u.Q12, Q13 = u.Q13, Q14 = u.Q14, Q15 = u.Q15, Q16 = u.Q16, Q17 = u.Q17, Q18 = u.Q18, Q19 = u.Q19, Q20 = u.Q20, Q21 = u.Q21, Q22 = u.Q22, Q23 = u.Q23, Q24 = u.Q24, Q25 = u.Q25, Q26 = u.Q26, Q27 = u.Q27, Q28 = u.Q28, Q29 = u.Q29, Q30 = u.Q30, Q31 = u.Q31, Q32 = u.Q32, Q33 = u.Q33, Q34 = u.Q34, Q35 = u.Q35, Q36 = u.Q36, Q37 = u.Q37, Q38 = u.Q38, Q39 = u.Q39, Q40 = u.Q40, Q41 = u.Q41, Q42 = u.Q42, Q43 = u.Q43, Q44 = u.Q44, Q45 = u.Q45, Q46 = u.Q46, Q47 = u.Q47, Q48 = u.Q48, Q49 = u.Q49, Q50 = u.Q50, Q51 = u.Q51, Q52 = u.Q52, Q53 = u.Q53, Q54 = u.Q54, Q55 = u.Q55, Q56 = u.Q56, Q57 = u.Q57, Q58 = u.Q58, Q59 = u.Q59, Q60 = u.Q60, Q61 = u.Q61, Q62 = u.Q62, Q63 = u.Q63, Q64 = u.Q64, Q65 = u.Q65, Q66 = u.Q66, Q67 = u.Q67, Q68 = u.Q68, Q69 = u.Q69, Q70 = u.Q70, Q71 = u.Q71, Q72 = u.Q72, Q73 = u.Q73, Q74 = u.Q74, Q75 = u.Q75, Q76 = u.Q76, Q77 = u.Q77, Q78 = u.Q78, Q79 = u.Q79, Q80 = u.Q80, Q81 = u.Q81, Q82 = u.Q82, Q83 = u.Q83, Q84 = u.Q84, Q85 = u.Q85, Q86 = u.Q86, Q87 = u.Q87, Q88 = u.Q88, Q89 = u.Q89, Q90 = u.Q90, Q91 = u.Q91, Q92 = u.Q92, Q93 = u.Q93, Q94 = u.Q94, Q95 = u.Q95, Q96 = u.Q96, Q97 = u.Q97, Q98 = u.Q98, Q99 = u.Q99, Q100 = u.Q100, Correct = u.Correct, Incorrect = u.Incorrect, NA = u.NA, LeftBlank = u.LeftBlank, MarksObt = u.MarksObt, [Rank] = u.[Rank]
                              FROM dbo.OmrResult r
                              INNER JOIN #OmrUpload u ON u.RegNo = r.RegNo AND u.Paper = r.Paper;

                              INSERT INTO dbo.OmrResult (SNo, RegNo, Version, Barcode, Paper, Dist, Centre, Q1, Q2, Q3, Q4, Q5, Q6, Q7, Q8, Q9, Q10, Q11, Q12, Q13, Q14, Q15, Q16, Q17, Q18, Q19, Q20, Q21, Q22, Q23, Q24, Q25, Q26, Q27, Q28, Q29, Q30, Q31, Q32, Q33, Q34, Q35, Q36, Q37, Q38, Q39, Q40, Q41, Q42, Q43, Q44, Q45, Q46, Q47, Q48, Q49, Q50, Q51, Q52, Q53, Q54, Q55, Q56, Q57, Q58, Q59, Q60, Q61, Q62, Q63, Q64, Q65, Q66, Q67, Q68, Q69, Q70, Q71, Q72, Q73, Q74, Q75, Q76, Q77, Q78, Q79, Q80, Q81, Q82, Q83, Q84, Q85, Q86, Q87, Q88, Q89, Q90, Q91, Q92, Q93, Q94, Q95, Q96, Q97, Q98, Q99, Q100, Correct, Incorrect, NA, LeftBlank, MarksObt, [Rank])
                              SELECT u.SNo, u.RegNo, u.Version, u.Barcode, u.Paper, u.Dist, u.Centre, u.Q1, u.Q2, u.Q3, u.Q4, u.Q5, u.Q6, u.Q7, u.Q8, u.Q9, u.Q10, u.Q11, u.Q12, u.Q13, u.Q14, u.Q15, u.Q16, u.Q17, u.Q18, u.Q19, u.Q20, u.Q21, u.Q22, u.Q23, u.Q24, u.Q25, u.Q26, u.Q27, u.Q28, u.Q29, u.Q30, u.Q31, u.Q32, u.Q33, u.Q34, u.Q35, u.Q36, u.Q37, u.Q38, u.Q39, u.Q40, u.Q41, u.Q42, u.Q43, u.Q44, u.Q45, u.Q46, u.Q47, u.Q48, u.Q49, u.Q50, u.Q51, u.Q52, u.Q53, u.Q54, u.Q55, u.Q56, u.Q57, u.Q58, u.Q59, u.Q60, u.Q61, u.Q62, u.Q63, u.Q64, u.Q65, u.Q66, u.Q67, u.Q68, u.Q69, u.Q70, u.Q71, u.Q72, u.Q73, u.Q74, u.Q75, u.Q76, u.Q77, u.Q78, u.Q79, u.Q80, u.Q81, u.Q82, u.Q83, u.Q84, u.Q85, u.Q86, u.Q87, u.Q88, u.Q89, u.Q90, u.Q91, u.Q92, u.Q93, u.Q94, u.Q95, u.Q96, u.Q97, u.Q98, u.Q99, u.Q100, u.Correct, u.Incorrect, u.NA, u.LeftBlank, u.MarksObt, u.[Rank]
                              FROM #OmrUpload u
                              WHERE NOT EXISTS (SELECT 1 FROM dbo.OmrResult r
                                                WHERE r.RegNo = u.RegNo AND r.Paper = u.Paper);",
                            con, transaction))
                        {
                            cmd.CommandTimeout = SqlTimeoutSeconds;
                            cmd.Parameters.Add("@ExamCode", SqlDbType.VarChar, 5).Value = examCode;

                            using (SqlDataReader dr = cmd.ExecuteReader())
                            {
                                dr.Read();
                                counts.Replaced = dr.GetInt32(0);
                            }
                        }

                        counts.Added = counts.Rows - counts.Replaced;
                        transaction.Commit();
                    }
                    catch
                    {
                        RollBack(transaction);
                        throw;
                    }
                }
            }

            return counts;
        }

        /// <summary>One result row checked and converted, or null (with errors added).</summary>
        private static DataRow ReadRow(DataTable batch, int rowNumber, string[] cells, HashSet<string> examPapers,
                                       HashSet<string> seen, List<string> errors)
        {
            Func<int, string> at = i => i < cells.Length ? Clean(cells[i]) : "";
            int before = errors.Count;

            string regNo = Number(at(1));
            string version = at(2).ToUpperInvariant();
            string barcode = Number(at(3));
            string paper = at(4).ToUpperInvariant();
            string district = at(5);
            string centre = at(6);

            if (!RegNoPattern.IsMatch(regNo))
                errors.Add(RowError(rowNumber, "RegNo must be 1 to 10 letters or digits."));

            if (paper != "P1" && paper != "P2")
                errors.Add(RowError(rowNumber, "Paper must be P1 or P2."));
            else if (!examPapers.Contains(paper))
                errors.Add(RowError(rowNumber, "This examination has no paper " + paper + "."));
            else if (!KeySets.IsValid(paper, version))
                errors.Add(RowError(rowNumber, "Version for " + paper + " must be one letter " + KeySets.Range(paper) + "."));

            if (barcode.Length > 20)
                errors.Add(RowError(rowNumber, "Barcode cannot exceed 20 characters."));

            if (district.Length > 100 || centre.Length > 200)
                errors.Add(RowError(rowNumber, "Dist cannot exceed 100 characters and Centre 200."));

            string[] answers = new string[Questions];

            for (int q = 0; q < Questions; q++)
            {
                string value = Number(at(7 + q));

                // Blank, one circle (1 to 5) or several circles as digits (e.g. 15, 1234).
                if (value.Length > 0 && !MarkedAnswer.IsMatch(value))
                {
                    errors.Add(RowError(rowNumber, "Q" + (q + 1) + " must be blank or digits 1 to 5 (for example 3 or 15), not \"" +
                        (value.Length > 10 ? value.Substring(0, 10) + "..." : value) + "\"."));
                    break;
                }

                answers[q] = value;
            }

            int correct = 0, incorrect = 0, notAttempted = 0, leftBlank = 0, sNo, rank;
            decimal marks;
            bool hasSNo = TryInt(at(0), out sNo);
            bool hasRank = TryInt(at(112), out rank);

            if (!TryInt(at(107), out correct) || !TryInt(at(108), out incorrect) ||
                !TryInt(at(109), out notAttempted) || !TryInt(at(110), out leftBlank))
            {
                errors.Add(RowError(rowNumber, "Cr., InCr., NA. and LeftBlank must be whole numbers."));
            }

            if (!decimal.TryParse(at(111), NumberStyles.Number, CultureInfo.InvariantCulture, out marks) ||
                marks < -1000 || marks > 1000)
            {
                errors.Add(RowError(rowNumber, "MarksObt. must be a number."));
            }

            if (at(112).Length > 0 && !hasRank)
                errors.Add(RowError(rowNumber, "Rank must be a whole number."));

            if (errors.Count == before && !seen.Add(regNo + "|" + paper))
                errors.Add(RowError(rowNumber, "RegNo " + regNo + " appears more than once for paper " + paper + "."));

            if (errors.Count > before)
                return null;

            DataRow row = batch.NewRow();
            row["SNo"] = hasSNo ? (object)sNo : DBNull.Value;
            row["RegNo"] = regNo;
            row["Version"] = version;
            row["Barcode"] = barcode.Length == 0 ? (object)DBNull.Value : barcode;
            row["Paper"] = paper;
            row["Dist"] = district.Length == 0 ? (object)DBNull.Value : district;
            row["Centre"] = centre.Length == 0 ? (object)DBNull.Value : centre;

            // A question left blank is stored as NULL.
            for (int q = 0; q < Questions; q++)
                row["Q" + (q + 1)] = answers[q].Length == 0 ? (object)DBNull.Value : answers[q];

            row["Correct"] = correct;
            row["Incorrect"] = incorrect;
            row["NA"] = notAttempted;
            row["LeftBlank"] = leftBlank;
            row["MarksObt"] = marks;
            row["Rank"] = hasRank ? (object)rank : DBNull.Value;
            return row;
        }

        /// <summary>A batch of rows in the layout of dbo.OmrResult (and of the file).</summary>
        private static DataTable NewBatch()
        {
            DataTable t = new DataTable();
            t.Columns.Add("SNo", typeof(int));
            t.Columns.Add("RegNo", typeof(string));
            t.Columns.Add("Version", typeof(string));
            t.Columns.Add("Barcode", typeof(string));
            t.Columns.Add("Paper", typeof(string));
            t.Columns.Add("Dist", typeof(string));
            t.Columns.Add("Centre", typeof(string));

            for (int q = 1; q <= Questions; q++)
                t.Columns.Add("Q" + q, typeof(string));

            t.Columns.Add("Correct", typeof(int));
            t.Columns.Add("Incorrect", typeof(int));
            t.Columns.Add("NA", typeof(int));
            t.Columns.Add("LeftBlank", typeof(int));
            t.Columns.Add("MarksObt", typeof(decimal));
            t.Columns.Add("Rank", typeof(int));
            return t;
        }

        // Accepted spellings of each header, compared without spaces, dots,
        // underscores or case.
        private static readonly string[][] FixedHeaders =
        {
            new[] { "SNO", "SLNO", "SERIALNO" },
            new[] { "REGNO", "HALLTICKETNO", "REGISTRATIONNO", "REGISTERNO" },
            new[] { "VERSION", "VERSIONCODE" },
            new[] { "BARCODE", "BARCODENO" },
            new[] { "PAPER", "PAPERCODE" },
            new[] { "DIST", "DISTRICT" },
            new[] { "CENTRE", "CENTER", "CENTRENAME" }
        };

        private static readonly string[][] TotalHeaders =
        {
            new[] { "CR", "CORRECT" },
            new[] { "INCR", "INCORRECT", "WRONG" },
            new[] { "NA", "NOTATTEMPTED" },
            new[] { "LEFTBLANK", "BLANK" },
            new[] { "MARKSOBT", "MARKSOBTAINED", "MARKS" },
            new[] { "RANK" }
        };

        private static readonly string[] TotalNames = { "Cr.", "InCr.", "NA.", "LeftBlank", "MarksObt.", "Rank" };
        private static readonly string[] FixedNames = { "S.No", "RegNo", "Version", "Barcode", "Paper", "Dist", "Centre" };

        private static bool CheckHeaders(string[] cells, List<string> errors)
        {
            Func<int, string> header = i => i < cells.Length ? Regex.Replace(Clean(cells[i]), @"[\s\._]", "").ToUpperInvariant() : "";

            for (int i = 0; i < FixedHeaders.Length; i++)
            {
                if (!FixedHeaders[i].Contains(header(i)))
                    errors.Add("Invalid Excel header in column " + (i + 1) + ". Expected: " + FixedNames[i]);
            }

            for (int q = 1; q <= Questions; q++)
            {
                string h = header(6 + q);

                if (h != "Q" + q && h != q.ToString(CultureInfo.InvariantCulture))
                {
                    errors.Add("Invalid Excel header in column " + (7 + q) + ". Expected: Q" + q);
                    break;
                }
            }

            for (int i = 0; i < TotalHeaders.Length; i++)
            {
                if (!TotalHeaders[i].Contains(header(107 + i)))
                    errors.Add("Invalid Excel header in column " + (108 + i) + ". Expected: " + TotalNames[i]);
            }

            return errors.Count == 0;
        }

        // ---------------------------------------------------------
        // DELETE A PAPER'S RESULTS
        // ---------------------------------------------------------

        protected void gvSummary_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "DeletePaper")
                return;

            string examCode = SelectedExamCode;
            string paper = Convert.ToString(e.CommandArgument);

            if (examCode.Length == 0 || (paper != "P1" && paper != "P2"))
                return;

            try
            {
                // Only this examination's candidates: dbo.OmrResult has no
                // examination column, and other examinations use the same papers.
                int deleted = sh.ExecuteNonQuery(
                    @"DELETE r FROM dbo.OmrResult r
                      INNER JOIN dbo.MarksCandidate c ON c.RegNo = r.RegNo AND c.ExamCode = @ExamCode
                      WHERE r.Paper = @PaperCode",
                    new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode },
                    new SqlParameter("@PaperCode", SqlDbType.VarChar, 2) { Value = paper });

                AdminAudit.Record(Context, AdminAudit.DeleteResults, "Exam " + examCode + ", paper " + paper + ": " + deleted + " results deleted");
                ShowMessage(deleted.ToString("N0", CultureInfo.InvariantCulture) + " results of paper " + paper + " deleted for this examination.", true);
            }
            catch (Exception ex)
            {
                ShowMessage(ErrorReport.Handle(ex, "Result delete failed", "The results could not be deleted. Please try again."), false);
            }

            BindSummary();
        }

        // ---------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------

        /// <summary>
        /// The uploaded file as a seekable stream, which the .xlsx reader needs.
        /// ASP.NET's upload stream normally is (a large upload is buffered on
        /// disk); otherwise it is copied to memory.
        /// </summary>
        private Stream SeekableUpload()
        {
            Stream input = fuResults.PostedFile.InputStream;

            if (input.CanSeek)
            {
                input.Position = 0;
                return input;
            }

            MemoryStream copy = new MemoryStream();
            input.CopyTo(copy);
            copy.Position = 0;
            return copy;
        }

        private HashSet<string> ExamPapers(string examCode)
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT DISTINCT UPPER(LTRIM(RTRIM(PaperCode))) AS PaperCode FROM dbo.Subject WHERE ExamCode = @ExamCode",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode });

            return new HashSet<string>(dt.AsEnumerable().Select(r => r.Field<string>("PaperCode")), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Removes the stray quotes, tabs and spaces that exported files often carry.</summary>
        private static string Clean(string value)
        {
            return (value ?? "").Replace(' ', ' ').Trim(' ', '\t', '\r', '\n', '"', '\'');
        }

        /// <summary>"260000005.0" or "4.0" as stored by Excel for whole numbers becomes "260000005" or "4".</summary>
        private static readonly Regex MarkedAnswer = new Regex(@"^[1-5]{1,5}$");

        private static string Number(string value)
        {
            return WholeNumber.IsMatch(value) ? value.Split('.')[0] : value;
        }

        private static bool TryInt(string value, out int result)
        {
            decimal d;
            result = 0;

            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ||
                d != decimal.Truncate(d) || d < 0 || d > int.MaxValue)
            {
                return false;
            }

            result = (int)d;
            return true;
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

        private static void BulkCopy(SqlConnection con, SqlTransaction transaction, DataTable rows)
        {
            using (SqlBulkCopy bulk = new SqlBulkCopy(con, SqlBulkCopyOptions.Default, transaction))
            {
                bulk.DestinationTableName = "#OmrUpload";
                bulk.BulkCopyTimeout = SqlTimeoutSeconds;

                foreach (DataColumn column in rows.Columns)
                    bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);

                bulk.WriteToServer(rows);
            }
        }

        /// <summary>Rollback that does not hide the real error when SQL Server already ended the transaction.</summary>
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

        private void ShowErrors(List<string> errors)
        {
            string text = "Upload cancelled; nothing was saved. " +
                (errors.Count >= MaxErrorsCollected ? "At least " : "") + errors.Count + " problem(s) found:<br />" +
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
    }
}
