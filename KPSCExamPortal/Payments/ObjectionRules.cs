using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using KPSCExamPortal.DLL;

namespace KPSCExamPortal.Payments
{
    /// <summary>Which objection window: each examination has one for each kind.</summary>
    public enum ObjectionKind
    {
        /// <summary>Answer key objections: dbo.Exam.ObjectionStart / ObjectionEnd.</summary>
        KeyAnswer,

        /// <summary>Marks objections: dbo.Exam.MarksObjectionStart / MarksObjectionEnd.</summary>
        Marks
    }

    /// <summary>The period during which objections may be filed for an examination.</summary>
    public class ObjectionWindow
    {
        public DateTime? Start;
        public DateTime? End;
        public bool IsOpen;

        /// <summary>Message for candidates when the window is not open.</summary>
        public string ClosedMessage
        {
            get
            {
                const string format = "dd-MMM-yyyy hh:mm tt";

                if (Start.HasValue && DateTime.Now < Start.Value)
                {
                    return "Objections can be filed from "
                        + Start.Value.ToString(format, CultureInfo.InvariantCulture)
                        + (End.HasValue ? " to " + End.Value.ToString(format, CultureInfo.InvariantCulture) : "")
                        + ".";
                }

                if (End.HasValue)
                {
                    return "The objection window closed on "
                        + End.Value.ToString(format, CultureInfo.InvariantCulture) + ".";
                }

                return "Objections are not open for this examination.";
            }
        }
    }

    public static class ObjectionRules
    {
        /// <summary>Fee per objection in rupees, from the ObjectionFee app setting.</summary>
        public static decimal Fee
        {
            get
            {
                decimal fee;

                if (!decimal.TryParse(ConfigurationManager.AppSettings["ObjectionFee"],
                        NumberStyles.Number, CultureInfo.InvariantCulture, out fee) || fee <= 0)
                {
                    throw new ConfigurationErrorsException("App setting 'ObjectionFee' is not set.");
                }

                return fee;
            }
        }

        /// <summary>The dbo.Exam columns holding the start and end of a kind's window.</summary>
        public static string StartColumn(ObjectionKind kind)
        {
            return kind == ObjectionKind.Marks ? "MarksObjectionStart" : "ObjectionStart";
        }

        public static string EndColumn(ObjectionKind kind)
        {
            return kind == ObjectionKind.Marks ? "MarksObjectionEnd" : "ObjectionEnd";
        }

        /// <summary>
        /// Window dates live in dbo.Exam: ObjectionStart / ObjectionEnd for answer
        /// key objections, MarksObjectionStart / MarksObjectionEnd for marks
        /// objections. The database clock decides whether it is open.
        /// </summary>
        public static ObjectionWindow GetWindow(string examCode, ObjectionKind kind)
        {
            // Column names come from StartColumn / EndColumn only, never from input.
            string start = StartColumn(kind), end = EndColumn(kind);

            DataTable dt = new SqlHelper().ExecuteDataTable(
                "SELECT " + start + " AS ObjectionStart, " + end + " AS ObjectionEnd, " +
                "CASE WHEN (" + start + " IS NULL OR " + start + " <= GETDATE()) " +
                "AND (" + end + " IS NULL OR " + end + " >= GETDATE()) THEN 1 ELSE 0 END AS IsOpen " +
                "FROM dbo.Exam WHERE ExamCode = @ExamCode AND IsActive = 'Y'",
                new SqlParameter("@ExamCode", SqlDbType.VarChar, 5) { Value = examCode ?? "" });

            ObjectionWindow window = new ObjectionWindow();

            if (dt.Rows.Count == 0)
                return window;

            DataRow row = dt.Rows[0];

            window.Start = row["ObjectionStart"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ObjectionStart"]);
            window.End = row["ObjectionEnd"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ObjectionEnd"]);
            window.IsOpen = Convert.ToInt32(row["IsOpen"]) == 1;

            return window;
        }
    }
}
