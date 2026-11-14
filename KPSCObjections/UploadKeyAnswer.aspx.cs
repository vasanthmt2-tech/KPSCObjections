
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Web.UI;
using System.Web.UI.WebControls;
using ClosedXML.Excel;
using KPSCObjections.DLL;

namespace KPSCObjections
{
    public partial class UploadKeyAnswer : KPSCObjections.Security.AdminPage
    {
        SqlHelper sh = new SqlHelper();

        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings[
                "Objection_ConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindExamination();
                BindUploadedDocuments();
            }
        }

        // ---------------------------------------------------------
        // BIND EXAMINATION DROPDOWN
        // ---------------------------------------------------------

        public void BindExamination()
        {
            DataTable dt = sh.ExecuteDataTable(
                "SELECT ExamCode, ExamDescription FROM Exam");

            bindDropDownList(ddlExamCode, dt);
        }

        public void bindDropDownList(
            ListControl lstcontrol,
            DataTable dt)
        {
            lstcontrol.Items.Clear();

            lstcontrol.DataSource = dt;
            lstcontrol.DataValueField = dt.Columns[0].ToString();
            lstcontrol.DataTextField = dt.Columns[1].ToString();

            lstcontrol.DataBind();

            lstcontrol.Items.Insert(
                0, new ListItem("---SELECT---", "---SELECT---"));
        }

        // ---------------------------------------------------------
        // BIND UPLOADED DOCUMENT GRID
        // ---------------------------------------------------------

        private void BindUploadedDocuments()
        {
            using (SqlConnection con =
                new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        DocumentId,
                        ExamCode,
                        SubjectCode,
                        FileName,
                        UploadedOn
                    FROM dbo.UploadedKeyAnswerDocuments
                    ORDER BY UploadedOn DESC";

                using (SqlDataAdapter da =
                    new SqlDataAdapter(query, con))
                {
                    DataTable dt = new DataTable();

                    da.Fill(dt);

                    gvUploadedDocuments.DataSource = dt;
                    gvUploadedDocuments.DataBind();
                }
            }
        }

        // ---------------------------------------------------------
        // UPLOAD KEY ANSWER EXCEL FILE
        // ---------------------------------------------------------

        protected void btnUpload_Click(object sender, EventArgs e)
        {
            lblMessage.Text = "";

            string savedFilePath = null;
            bool uploadCommitted = false;

            try
            {
                // 1. Validate Examination

                if (ddlExamCode.SelectedValue == "---SELECT---")
                {
                    ShowMessage(
                        "Please select Examination.", false);
                    return;
                }

                // 2. Validate Subject Code

                if (ddlSubjectCode.SelectedValue == "---SELECT---")
                {
                    ShowMessage(
                        "Please select Subject Code.", false);
                    return;
                }

                string examCode =
                    ddlExamCode.SelectedValue.Trim();

                string subjectCode =
                    ddlSubjectCode.SelectedValue.Trim();

                // 3. Validate Excel File

                if (!fuKeyAnswers.HasFile)
                {
                    ShowMessage(
                        "Please select an Excel file.", false);
                    return;
                }

                string extension =
                    Path.GetExtension(
                        fuKeyAnswers.FileName).ToLowerInvariant();

                if (extension != ".xlsx")
                {
                    ShowMessage(
                        "Only .xlsx Excel files are allowed.", false);
                    return;
                }

                if (fuKeyAnswers.PostedFile.ContentLength == 0)
                {
                    ShowMessage(
                        "The selected Excel file is empty.", false);
                    return;
                }

                if (fuKeyAnswers.PostedFile.ContentLength >
                    5 * 1024 * 1024)
                {
                    ShowMessage(
                        "Excel file size cannot exceed 5 MB.", false);
                    return;
                }

                // Read file bytes once.
                // These bytes will be used for both Excel processing
                // and saving the original document.

                byte[] excelBytes = fuKeyAnswers.FileBytes;

                // 4. Read Excel File

                List<string[]> answerRows =
                    new List<string[]>();

                using (MemoryStream excelStream =
                    new MemoryStream(excelBytes))
                using (XLWorkbook workbook =
                    new XLWorkbook(excelStream))
                {
                    IXLWorksheet worksheet =
                        workbook.Worksheet(1);

                    // Expected 14 columns

                    string[] expectedHeaders =
                    {
                        "M",
                        "Key Answer",
                        "Set A",
                        "Set B",
                        "Set C",
                        "Set D",
                        "Set E",
                        "Set F",
                        "Set G",
                        "Set H",
                        "Set I",
                        "Set J",
                        "Set K",
                        "Set L"
                    };

                    // 5. Validate Excel Headers

                    for (int col = 1; col <= 14; col++)
                    {
                        string actualHeader =
                            NormalizeHeader(
                                worksheet.Cell(1, col)
                                    .GetFormattedString());

                        string expectedHeader =
                            NormalizeHeader(
                                expectedHeaders[col - 1]);

                        if (actualHeader != expectedHeader)
                        {
                            ShowMessage(
                                "Invalid Excel header in column " +
                                col + ". Expected: " +
                                expectedHeaders[col - 1],
                                false);

                            return;
                        }
                    }

                    // 6. Read Question Rows

                    int lastRow =
                        worksheet.LastRowUsed() == null
                            ? 1
                            : worksheet.LastRowUsed().RowNumber();

                    HashSet<int> questionNumbers =
                        new HashSet<int>();

                    for (int row = 2; row <= lastRow; row++)
                    {
                        bool isEmpty = true;

                        for (int col = 1; col <= 14; col++)
                        {
                            if (!worksheet.Cell(row, col).IsEmpty())
                            {
                                isEmpty = false;
                                break;
                            }
                        }

                        if (isEmpty)
                            continue;

                        string questionText =
                            worksheet.Cell(row, 1)
                                .GetFormattedString().Trim();

                        int questionNo;

                        if (!int.TryParse(
                            questionText,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out questionNo) || questionNo <= 0)
                        {
                            ShowMessage(
                                "Invalid Question Number at Excel row " +
                                row, false);

                            return;
                        }

                        if (!questionNumbers.Add(questionNo))
                        {
                            ShowMessage(
                                "Duplicate Question Number " +
                                questionNo +
                                " found at Excel row " + row,
                                false);

                            return;
                        }

                        string[] values = new string[14];

                        for (int col = 1; col <= 14; col++)
                        {
                            values[col - 1] =
                                worksheet.Cell(row, col)
                                    .GetFormattedString().Trim();
                        }

                        // Validate answer lengths

                        for (int col = 2; col <= 14; col++)
                        {
                            if (values[col - 1].Length > 20)
                            {
                                ShowMessage(
                                    "Answer exceeds 20 characters at " +
                                    "Excel row " + row +
                                    ", column " + col,
                                    false);

                                return;
                            }
                        }

                        answerRows.Add(values);
                    }
                }

                if (answerRows.Count == 0)
                {
                    ShowMessage(
                        "No question records found in the Excel file.",
                        false);

                    return;
                }

                // 7. Save Original Excel File

                string uploadFolder =
                    Server.MapPath("~/UploadedKeyAnswers/");

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string originalFileName =
                    Path.GetFileName(fuKeyAnswers.FileName);

                string storedFileName =
                    Guid.NewGuid().ToString("N") + ".xlsx";

                savedFilePath =
                    Path.Combine(uploadFolder, storedFileName);

                File.WriteAllBytes(savedFilePath, excelBytes);

                // 8. Insert Key Answers and Document Details
                //    in a Single SQL Transaction

                using (SqlConnection con =
                    new SqlConnection(connectionString))
                {
                    con.Open();

                    using (SqlTransaction transaction =
                        con.BeginTransaction())
                    {
                        try
                        {
                            // Check duplicate upload

                            using (SqlCommand checkCmd =
                                new SqlCommand(
                                    @"SELECT COUNT(1)
                                      FROM dbo.KeyAnswers
                                      WHERE ExamCode = @ExamCode
                                      AND SubjectCode = @SubjectCode",
                                    con, transaction))
                            {
                                checkCmd.Parameters.Add(
                                    "@ExamCode",
                                    SqlDbType.VarChar, 20).Value =
                                    examCode;

                                checkCmd.Parameters.Add(
                                    "@SubjectCode",
                                    SqlDbType.VarChar, 5).Value =
                                    subjectCode;

                                int existingRecords =
                                    Convert.ToInt32(
                                        checkCmd.ExecuteScalar());

                                if (existingRecords > 0)
                                {
                                    throw new Exception(
                                        "Key answers already exist for " +
                                        "the selected Exam Code and " +
                                        "Subject Code. Upload cancelled.");
                                }
                            }

                            // Insert Key Answers

                            string insertQuery = @"
                                INSERT INTO dbo.KeyAnswers
                                (
                                    ExamCode,
                                    SubjectCode,
                                    QuestionNo,
                                    MasterAnswer,
                                    SetA, SetB, SetC, SetD,
                                    SetE, SetF, SetG, SetH,
                                    SetI, SetJ, SetK, SetL
                                )
                                VALUES
                                (
                                    @ExamCode,
                                    @SubjectCode,
                                    @QuestionNo,
                                    @MasterAnswer,
                                    @SetA, @SetB, @SetC, @SetD,
                                    @SetE, @SetF, @SetG, @SetH,
                                    @SetI, @SetJ, @SetK, @SetL
                                )";

                            using (SqlCommand cmd =
                                new SqlCommand(
                                    insertQuery, con, transaction))
                            {
                                cmd.Parameters.Add(
                                    "@ExamCode",
                                    SqlDbType.VarChar, 5);

                                cmd.Parameters.Add(
                                    "@SubjectCode",
                                    SqlDbType.VarChar, 5);

                                cmd.Parameters.Add(
                                    "@QuestionNo",
                                    SqlDbType.Int);

                                cmd.Parameters.Add(
                                    "@MasterAnswer",
                                    SqlDbType.Int);

                                string[] setNames =
                                {
                                    "@SetA", "@SetB",
                                    "@SetC", "@SetD",
                                    "@SetE", "@SetF",
                                    "@SetG", "@SetH",
                                    "@SetI", "@SetJ",
                                    "@SetK", "@SetL"
                                };

                                foreach (string setName in setNames)
                                {
                                    cmd.Parameters.Add(
                                        setName,
                                        SqlDbType.VarChar, 20);
                                }

                                foreach (string[] values in answerRows)
                                {
                                    cmd.Parameters["@ExamCode"].Value =
                                        examCode;

                                    cmd.Parameters["@SubjectCode"].Value =
                                        subjectCode;

                                    cmd.Parameters["@QuestionNo"].Value =
                                        int.Parse(values[0]);

                                    cmd.Parameters["@MasterAnswer"].Value =
                                        GetDatabaseValue(values[1]);

                                    for (int i = 0; i < 12; i++)
                                    {
                                        cmd.Parameters[setNames[i]].Value =
                                            GetDatabaseValue(
                                                values[i + 2]);
                                    }

                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // Insert Uploaded Document Details

                            string documentQuery = @"
                                INSERT INTO dbo.UploadedKeyAnswerDocuments
                                (
                                    ExamCode,
                                    SubjectCode,
                                    FileName,
                                    FilePath
                                )
                                VALUES
                                (
                                    @ExamCode,
                                    @SubjectCode,
                                    @FileName,
                                    @FilePath
                                )";

                            using (SqlCommand documentCmd =
                                new SqlCommand(
                                    documentQuery, con, transaction))
                            {
                                documentCmd.Parameters.Add(
                                    "@ExamCode",
                                    SqlDbType.VarChar, 50).Value =
                                    examCode;

                                documentCmd.Parameters.Add(
                                    "@SubjectCode",
                                    SqlDbType.VarChar, 5).Value =
                                    subjectCode;

                                documentCmd.Parameters.Add(
                                    "@FileName",
                                    SqlDbType.NVarChar, 255).Value =
                                    originalFileName;

                                documentCmd.Parameters.Add(
                                    "@FilePath",
                                    SqlDbType.NVarChar, 500).Value =
                                    storedFileName;

                                documentCmd.ExecuteNonQuery();
                            }

                            // Commit both insert operations

                            transaction.Commit();

                            uploadCommitted = true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                // 9. Upload Successful

                ShowMessage(
                    answerRows.Count +
                    " key answer records and Excel document " +
                    "uploaded successfully.",
                    true);

                BindUploadedDocuments();
            }
            catch (Exception ex)
            {
                // Delete saved file only if database transaction
                // was not successfully committed.

                if (!uploadCommitted &&
                    !string.IsNullOrEmpty(savedFilePath) &&
                    File.Exists(savedFilePath))
                {
                    try
                    {
                        File.Delete(savedFilePath);
                    }
                    catch
                    {
                        // File cleanup failed.
                    }
                }

                if (uploadCommitted)
                {
                    ShowMessage(
                        "Upload was successful, but an error occurred " +
                        "while refreshing the document grid: " +
                        ex.Message,
                        false);
                }
                else
                {
                    ShowMessage(
                        "Upload failed: " + ex.Message,
                        false);
                }
            }
        }

        // ---------------------------------------------------------
        // DOWNLOAD AND DELETE GRID COMMANDS
        // ---------------------------------------------------------

        protected void gvUploadedDocuments_RowCommand(
            object sender,
            GridViewCommandEventArgs e)
        {
            if (e.CommandName != "DownloadFile" &&
                e.CommandName != "DeleteFile")
            {
                return;
            }

            int documentId;

            if (!int.TryParse(
                Convert.ToString(e.CommandArgument),
                out documentId))
            {
                ShowMessage(
                    "Invalid document selection.", false);

                return;
            }

            try
            {
                if (e.CommandName == "DownloadFile")
                {
                    DownloadDocument(documentId);
                }
                else if (e.CommandName == "DeleteFile")
                {
                    DeleteDocument(documentId);
                }
            }
            catch (Exception ex)
            {
                ShowMessage(
                    "Operation failed: " + ex.Message, false);
            }
        }

        // ---------------------------------------------------------
        // DOWNLOAD DOCUMENT
        // ---------------------------------------------------------

        private void DownloadDocument(int documentId)
        {
            string fileName = "";
            string storedFileName = "";

            using (SqlConnection con =
                new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT FileName, FilePath
                    FROM dbo.UploadedKeyAnswerDocuments
                    WHERE DocumentId = @DocumentId";

                using (SqlCommand cmd =
                    new SqlCommand(query, con))
                {
                    cmd.Parameters.Add(
                        "@DocumentId",
                        SqlDbType.Int).Value = documentId;

                    con.Open();

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read())
                        {
                            ShowMessage(
                                "Document not found.", false);
                            return;
                        }

                        fileName =
                            Convert.ToString(dr["FileName"]);

                        storedFileName =
                            Convert.ToString(dr["FilePath"]);
                    }
                }
            }

            // Prevent directory traversal

            storedFileName =
                Path.GetFileName(storedFileName);

            string uploadFolder =
                Server.MapPath("~/UploadedKeyAnswers/");

            string fullPath =
                Path.Combine(uploadFolder, storedFileName);

            if (!File.Exists(fullPath))
            {
                ShowMessage(
                    "The uploaded document is missing from the server.",
                    false);

                return;
            }

            string downloadFileName =
                Path.GetFileName(fileName)
                    .Replace("\"", "")
                    .Replace("\r", "")
                    .Replace("\n", "");

            Response.Clear();
            Response.Buffer = true;

            Response.ContentType =
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            Response.AddHeader(
                "Content-Disposition",
                "attachment; filename=\"" +
                downloadFileName + "\"");

            Response.TransmitFile(fullPath);
            Response.Flush();

            // Stop the page's own HTML being appended to the downloaded file.
            Response.SuppressContent = true;

            Context.ApplicationInstance.CompleteRequest();
        }

        // ---------------------------------------------------------
        // DELETE DOCUMENT
        // ---------------------------------------------------------


        private void DeleteDocument(int documentId)
        {
            string storedFileName = "";
            string examCode = "";
            string subjectCode = "";
            int deletedKeyAnswerCount = 0;

            using (SqlConnection con =
                new SqlConnection(connectionString))
            {
                con.Open();

                using (SqlTransaction transaction =
                    con.BeginTransaction())
                {
                    try
                    {
                        // 1. Get document details

                        string selectQuery = @"
                    SELECT ExamCode, SubjectCode, FilePath
                    FROM dbo.UploadedKeyAnswerDocuments
                    WHERE DocumentId = @DocumentId";

                        using (SqlCommand cmd =
                            new SqlCommand(selectQuery, con, transaction))
                        {
                            cmd.Parameters.Add(
                                "@DocumentId",
                                SqlDbType.Int).Value = documentId;

                            using (SqlDataReader dr = cmd.ExecuteReader())
                            {
                                if (!dr.Read())
                                {
                                    throw new Exception(
                                        "Uploaded document not found.");
                                }

                                examCode = Convert.ToString(dr["ExamCode"]);
                                subjectCode = Convert.ToString(dr["SubjectCode"]);
                                storedFileName = Convert.ToString(dr["FilePath"]);
                            }
                        }

                        // 2. Delete corresponding KeyAnswers records

                        string deleteKeyAnswersQuery = @"
                    DELETE FROM dbo.KeyAnswers
                    WHERE ExamCode = @ExamCode
                    AND SubjectCode = @SubjectCode";

                        using (SqlCommand cmd =
                            new SqlCommand(
                                deleteKeyAnswersQuery, con, transaction))
                        {
                            cmd.Parameters.Add(
                                "@ExamCode",
                                SqlDbType.VarChar, 5).Value = examCode;

                            cmd.Parameters.Add(
                                "@SubjectCode",
                                SqlDbType.VarChar, 5).Value = subjectCode;

                            deletedKeyAnswerCount =
                                cmd.ExecuteNonQuery();
                        }

                        // 3. Delete uploaded document database record

                        string deleteDocumentQuery = @"
                    DELETE FROM dbo.UploadedKeyAnswerDocuments
                    WHERE DocumentId = @DocumentId";

                        using (SqlCommand cmd =
                            new SqlCommand(
                                deleteDocumentQuery, con, transaction))
                        {
                            cmd.Parameters.Add(
                                "@DocumentId",
                                SqlDbType.Int).Value = documentId;

                            cmd.ExecuteNonQuery();
                        }

                        // 4. Commit both database deletions

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            // 5. Delete physical Excel file after database commit

            storedFileName = Path.GetFileName(storedFileName);

            string uploadFolder =
                Server.MapPath("~/UploadedKeyAnswers/");

            string fullPath =
                Path.Combine(uploadFolder, storedFileName);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            // 6. Refresh document grid

            ShowMessage(
                "Document deleted successfully. " +
                deletedKeyAnswerCount +
                " key-answer records deleted for Exam Code " +
                examCode +
                " and Subject Code " + subjectCode + ".",
                true);

            BindUploadedDocuments();
        }



        // ---------------------------------------------------------
        // EXCEL HEADER NORMALIZATION
        // ---------------------------------------------------------

        private string NormalizeHeader(string value)
        {
            return (value ?? "")
                .Trim()
                .Replace(" ", "")
                .ToUpperInvariant();
        }

        // ---------------------------------------------------------
        // DATABASE NULL HANDLING
        // ---------------------------------------------------------

        private object GetDatabaseValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DBNull.Value;

            return value.Trim();
        }

        // ---------------------------------------------------------
        // DISPLAY MESSAGE
        // ---------------------------------------------------------

        private void ShowMessage(
            string message,
            bool success)
        {
            lblMessage.Text = message;

            lblMessage.ForeColor =
                success ? Color.Green : Color.Red;
        }

        // ---------------------------------------------------------
        // EXAMINATION DROPDOWN SELECTED INDEX CHANGED
        // ---------------------------------------------------------

        protected void ddlExamCode_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (ddlExamCode.SelectedValue == "---SELECT---")
            {
                ddlSubjectCode.Items.Clear();

                ddlSubjectCode.Items.Insert(
                    0, new ListItem(
                        "---SELECT---", "---SELECT---"));

                ScriptManager.RegisterStartupScript(
                    this,
                    this.GetType(),
                    "alert",
                    "alert('Please select Examination.');",
                    true);

                return;
            }

            DataTable dt = sh.ExecuteDataTable(
                @"SELECT PaperCode, SubjectDescription
                  FROM Subject
                  WHERE ExamCode = @ExamCode
                  ORDER BY SubjectCode",
                "@ExamCode",
                "@" + ddlExamCode.SelectedValue);

            bindDropDownList(ddlSubjectCode, dt);
        }
    }
}
