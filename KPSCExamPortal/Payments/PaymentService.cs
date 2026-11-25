using System;
using System.Collections.Generic;
using System.Linq;
using KPSCExamPortal.Security;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using KPSCExamPortal.DLL;

namespace KPSCExamPortal.Payments
{
    public class PaymentInfo
    {
        public string RegNo;
        public string OrderNo;
        public decimal Amount;
        public string Status;
        public string SbiReferenceNo;
        public int AgeMinutes;
    }

    /// <summary>
    /// Payment lifecycle for objection fees.
    ///
    /// A payment is only ever marked SUCCESS or FAILED from SBI ePay's status
    /// query (double verification). The browser return and the server push are
    /// treated as prompts to run that query, never as proof of payment.
    /// </summary>
    public static class PaymentService
    {
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
        public const string Pending = "PENDING";

        /// <summary>
        /// Minutes after which an order SBI has no record of is treated as
        /// abandoned (the candidate never reached the payment page).
        /// </summary>
        private static int AbandonMinutes
        {
            get
            {
                int minutes;
                return int.TryParse(ConfigurationManager.AppSettings["SBIAbandonMinutes"], out minutes) && minutes > 0
                    ? minutes
                    : 30;
            }
        }

        /// <summary>
        /// Starts a payment covering all the candidate's draft objections. Earlier
        /// payments still being verified keep their own objections and do not
        /// block this one (migration 003). Returns OK, WINDOW_CLOSED or NO_DRAFTS.
        /// </summary>
        public static string Start(string regNo, out string orderNo, out decimal amount)
        {
            orderNo = SbiEpay.NewOrderNo();
            amount = 0;

            DataTable dt = new SqlHelper().ExecuteDataTable(
                "dbo.ExamPortalPaymentCreate",
                new SqlParameter("@RegNo", SqlDbType.VarChar, 10) { Value = regNo },
                new SqlParameter("@MerchantOrderNo", SqlDbType.VarChar, 50) { Value = orderNo },
                new SqlParameter("@FeePerObjection", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = ObjectionRules.Fee });

            string result = Convert.ToString(dt.Rows[0]["Result"]);

            if (result == "OK")
                amount = Convert.ToDecimal(dt.Rows[0]["Amount"]);

            return result;
        }

        public static PaymentInfo Find(string orderNo)
        {
            return Load(
                "SELECT RegNo, MerchantOrderNo, Amount, PaymentStatus, SBIReferenceNo, " +
                "DATEDIFF(MINUTE, CreatedDate, GETDATE()) AS AgeMinutes " +
                "FROM dbo.PaymentTransactions WHERE MerchantOrderNo = @Value",
                new SqlParameter("@Value", SqlDbType.VarChar, 50) { Value = orderNo ?? "" });
        }

        /// <summary>The candidate's payments still awaiting verification, oldest first (often none).</summary>
        public static List<PaymentInfo> FindAllPending(string regNo)
        {
            DataTable dt = new SqlHelper().ExecuteDataTable(
                "SELECT RegNo, MerchantOrderNo, Amount, PaymentStatus, SBIReferenceNo, " +
                "DATEDIFF(MINUTE, CreatedDate, GETDATE()) AS AgeMinutes " +
                "FROM dbo.PaymentTransactions WHERE RegNo = @Value AND PaymentStatus = 'PENDING' " +
                "ORDER BY PaymentId",
                new SqlParameter("@Value", SqlDbType.VarChar, 50) { Value = regNo ?? "" });

            return dt.Rows.Cast<DataRow>().Select(ToInfo).ToList();
        }

        /// <summary>
        /// Confirms every payment of the candidate still awaiting verification.
        /// Returns the status of each afterwards (SUCCESS / FAILED / PENDING).
        /// </summary>
        public static List<string> VerifyCandidatePending(string regNo)
        {
            return FindAllPending(regNo).Select(p => Verify(p.OrderNo)).ToList();
        }

        /// <summary>
        /// Confirms a pending payment with SBI ePay and records the outcome.
        /// Returns the payment's status afterwards, or null for an unknown order.
        /// </summary>
        public static string Verify(string orderNo)
        {
            PaymentInfo payment = Find(orderNo);

            if (payment == null)
                return null;

            if (payment.Status != Pending)
                return payment.Status;

            SbiResult result;

            try
            {
                result = SbiEpay.QueryStatus(payment.OrderNo, payment.Amount);
            }
            catch (Exception ex)
            {
                AppLog.Error("SBI status query failed for order " + payment.OrderNo + ": " + ex);

                return Pending;
            }

            string outcome = Pending;

            switch (result.Status)
            {
                case SbiStatus.Success:
                    if (Matches(result, payment) && result.Amount == payment.Amount)
                    {
                        outcome = Success;
                    }
                    else
                    {
                        System.Diagnostics.Trace.TraceError(
                            "SBI status query mismatch for order " + payment.OrderNo +
                            ": reply was for order '" + result.OrderNo + "', amount " + result.Amount + ".");
                    }
                    break;

                case SbiStatus.Failed:
                    if (result.IsQueryRefusal)
                    {
                        // SBI refused the status query (e.g. "Wrong Source": this
                        // server is not registered for the query API) or gave no
                        // transaction details. The candidate may well have paid, so
                        // the payment stays pending until a query really answers.
                        // Logged at most once an hour per order: the automatic
                        // re-check asks every few minutes.
                        if (System.Web.HttpRuntime.Cache.Add("sbi-refused:" + payment.OrderNo, true, null,
                                DateTime.UtcNow.AddHours(1), System.Web.Caching.Cache.NoSlidingExpiration,
                                System.Web.Caching.CacheItemPriority.Normal, null) == null)
                        {
                            AppLog.Write("WARN", "SBI status query for order " + payment.OrderNo +
                                         " was refused or gave no transaction (\"" + result.Reason +
                                         "\"); payment left pending. Check that this server's IP is " +
                                         "registered with SBI ePay for the status query.");
                        }
                    }
                    else if (Matches(result, payment))
                    {
                        outcome = Failed;
                    }
                    break;

                case SbiStatus.NotFound:
                    if (payment.AgeMinutes >= AbandonMinutes)
                        outcome = Failed;
                    break;

                case SbiStatus.Unknown:
                    // A reply that could not be read (wrong URL, an error page,
                    // a format change). The payment stays pending; logged at
                    // most once an hour per order, as the re-check repeats.
                    if (System.Web.HttpRuntime.Cache.Add("sbi-unreadable:" + payment.OrderNo, true, null,
                            DateTime.UtcNow.AddHours(1), System.Web.Caching.Cache.NoSlidingExpiration,
                            System.Web.Caching.CacheItemPriority.Normal, null) == null)
                    {
                        string status = System.Text.RegularExpressions.Regex.Replace(
                            result.RawStatus ?? "", @"\p{C}", " ").Trim();

                        AppLog.Write("WARN", "SBI status query for order " + payment.OrderNo +
                                     " gave a reply that could not be read" +
                                     (status.Length == 0 ? "" : " (status \"" + (status.Length > 40 ? status.Substring(0, 40) : status) + "\")") +
                                     "; payment left pending. Check SBIDoubleVerificationUrl and that SBI ePay answers it.");
                    }
                    break;
            }

            return Apply(payment.OrderNo, outcome, result);
        }

        /// <summary>Re-checks every pending payment. For the admin screen.</summary>
        public static void VerifyAllPending(out int succeeded, out int failed, out int stillPending)
        {
            succeeded = failed = stillPending = 0;

            DataTable dt = new SqlHelper().ExecuteDataTable(
                "SELECT MerchantOrderNo FROM dbo.PaymentTransactions WHERE PaymentStatus = 'PENDING' ORDER BY PaymentId");

            foreach (DataRow row in dt.Rows)
            {
                switch (Verify(Convert.ToString(row[0])))
                {
                    case Success: succeeded++; break;
                    case Failed: failed++; break;
                    default: stillPending++; break;
                }
            }
        }

        private static bool Matches(SbiResult result, PaymentInfo payment)
        {
            return string.Equals(result.OrderNo, payment.OrderNo, StringComparison.Ordinal)
                && string.Equals(result.MerchantId, SbiEpay.MerchantId, StringComparison.Ordinal);
        }

        private static string Apply(string orderNo, string outcome, SbiResult result)
        {
            DataTable dt = new SqlHelper().ExecuteDataTable(
                "dbo.PaymentApplyResult",
                new SqlParameter("@MerchantOrderNo", SqlDbType.VarChar, 50) { Value = orderNo },
                new SqlParameter("@ResultStatus", SqlDbType.VarChar, 20) { Value = outcome },
                new SqlParameter("@SBIReferenceNo", SqlDbType.VarChar, 100) { Value = DbValue(result.Atrn) },
                new SqlParameter("@PaymentMode", SqlDbType.VarChar, 30) { Value = DbValue(result.PayMode) },
                new SqlParameter("@PaymentDate", SqlDbType.DateTime2) { Value = (object)result.TransactionDate ?? DBNull.Value },
                new SqlParameter("@GatewayResponse", SqlDbType.NVarChar, -1) { Value = DbValue(result.Raw) });

            return dt.Rows.Count > 0 ? Convert.ToString(dt.Rows[0][0]) : null;
        }

        private static object DbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private static PaymentInfo Load(string query, SqlParameter parameter)
        {
            DataTable dt = new SqlHelper().ExecuteDataTable(query, parameter);

            if (dt.Rows.Count == 0)
                return null;

            return ToInfo(dt.Rows[0]);
        }

        private static PaymentInfo ToInfo(DataRow row)
        {
            return new PaymentInfo
            {
                RegNo = Convert.ToString(row["RegNo"]),
                OrderNo = Convert.ToString(row["MerchantOrderNo"]),
                Amount = Convert.ToDecimal(row["Amount"]),
                Status = Convert.ToString(row["PaymentStatus"]),
                SbiReferenceNo = Convert.ToString(row["SBIReferenceNo"]),
                AgeMinutes = Convert.ToInt32(row["AgeMinutes"])
            };
        }
    }
}
