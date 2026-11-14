using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using KPSCObjections.DLL;

namespace KPSCObjections.Payments
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
        /// Starts a payment covering all the candidate's draft objections.
        /// Returns OK, PENDING_EXISTS, WINDOW_CLOSED or NO_DRAFTS.
        /// </summary>
        public static string Start(string regNo, out string orderNo, out decimal amount)
        {
            orderNo = SbiEpay.NewOrderNo();
            amount = 0;

            DataTable dt = new SqlHelper().ExecuteDataTable(
                "dbo.PaymentCreate",
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

        /// <summary>The candidate's payment that is still awaiting verification, if any.</summary>
        public static PaymentInfo FindPending(string regNo)
        {
            return Load(
                "SELECT TOP 1 RegNo, MerchantOrderNo, Amount, PaymentStatus, SBIReferenceNo, " +
                "DATEDIFF(MINUTE, CreatedDate, GETDATE()) AS AgeMinutes " +
                "FROM dbo.PaymentTransactions WHERE RegNo = @Value AND PaymentStatus = 'PENDING' " +
                "ORDER BY PaymentId DESC",
                new SqlParameter("@Value", SqlDbType.VarChar, 50) { Value = regNo ?? "" });
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
                System.Diagnostics.Trace.TraceError(
                    "SBI status query failed for order " + payment.OrderNo + ": " + ex.Message);

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
                    if (Matches(result, payment))
                        outcome = Failed;
                    break;

                case SbiStatus.NotFound:
                    if (payment.AgeMinutes >= AbandonMinutes)
                        outcome = Failed;
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

            DataRow row = dt.Rows[0];

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
