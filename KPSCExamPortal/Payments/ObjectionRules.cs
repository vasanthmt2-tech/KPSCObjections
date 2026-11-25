using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using KPSCExamPortal.DLL;

namespace KPSCExamPortal.Payments
{
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

        /// <summary>
        /// Window dates live in dbo.Exam (ObjectionStart, ObjectionEnd). The
        /// database clock decides whether it is open.
        /// </summary>
        public static ObjectionWindow GetWindow(string examCode)
        {
            DataTable dt = new SqlHelper().ExecuteDataTable(
                "SELECT ObjectionStart, ObjectionEnd, " +
                "CASE WHEN (ObjectionStart IS NULL OR ObjectionStart <= GETDATE()) " +
                "AND (ObjectionEnd IS NULL OR ObjectionEnd >= GETDATE()) THEN 1 ELSE 0 END AS IsOpen " +
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
