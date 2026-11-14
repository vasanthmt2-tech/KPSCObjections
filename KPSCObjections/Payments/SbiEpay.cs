using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace KPSCObjections.Payments
{
    public enum SbiStatus
    {
        Success,
        Failed,
        Pending,
        NotFound,
        Unknown
    }

    /// <summary>A transaction outcome reported by SBI ePay.</summary>
    public class SbiResult
    {
        public SbiStatus Status = SbiStatus.Unknown;
        public string RawStatus;
        public string OrderNo;
        public string Atrn;
        public decimal? Amount;
        public string PayMode;
        public string MerchantId;
        public string Reason;
        public DateTime? TransactionDate;
        public string Raw;
    }

    /// <summary>
    /// SBI ePay aggregator-hosted integration, per Merchant Integration Guide 2.3.7
    /// and the .NET test kit (AES256.cs).
    /// </summary>
    public static class SbiEpay
    {
        private const int HttpTimeoutMs = 20000;

        public static string MerchantId { get { return Setting("SBIMerchantId"); } }
        public static string PaymentUrl { get { return Setting("SBIPaymentUrl"); } }

        // ---------------------------------------------------------------
        // Request
        // ---------------------------------------------------------------

        /// <summary>20 characters: K + yyMMddHHmmss + 7 random digits.</summary>
        public static string NewOrderNo()
        {
            byte[] bytes = new byte[4];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            int random = BitConverter.ToInt32(bytes, 0) & 0x7fffffff;

            return "K" + DateTime.Now.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture)
                + (random % 10000000).ToString("D7", CultureInfo.InvariantCulture);
        }

        public static string FormatAmount(decimal amount)
        {
            return amount.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>The encrypted 'EncryptTrans' value posted to SBI ePay.</summary>
        public static string BuildEncryptedRequest(string orderNo, decimal amount, string customerId)
        {
            // Merchant ID|Operating Mode|Country|Currency|Amount|Other Details|Success URL|
            // Fail URL|Aggregator ID|Order Number|Customer ID|Paymode|Access Medium|Transaction Source
            string plain = string.Join("|",
                MerchantId,
                Setting("SBIOperatingMode"),
                Setting("SBICountry"),
                Setting("SBICurrency"),
                FormatAmount(amount),
                "NA",
                Setting("SuccessURL"),
                Setting("FailureURL"),
                Setting("SBIAggregatorId"),
                orderNo,
                customerId,
                Setting("SBIPayMode"),
                Setting("SBIAccessMedium"),
                Setting("SBITransactionSource"));

            return new AES256().Encrypt(plain, EncryptionKey());
        }

        // ---------------------------------------------------------------
        // Browser / push response
        // ---------------------------------------------------------------

        /// <summary>
        /// Decrypts and parses 'encData' (browser) or 'pushRespData' (server push):
        /// Order No|ATRN|Status|Amount|Currency|Pay Mode|Other Details|Reason|Bank Code|
        /// Bank Ref|Transaction Date|Country|CIN|Merchant ID|...
        /// The result is a hint only; the payment is confirmed with QueryStatus.
        /// </summary>
        public static SbiResult ParseEncryptedResponse(string encrypted)
        {
            string plain = new AES256().Decrypt(encrypted, EncryptionKey());
            string[] f = plain.Split('|');

            SbiResult r = new SbiResult { Raw = plain };

            r.OrderNo = Field(f, 0);
            r.Atrn = Field(f, 1);
            r.RawStatus = Field(f, 2);
            r.Status = MapStatus(r.RawStatus);
            r.Amount = ParseAmount(Field(f, 3));
            r.PayMode = Field(f, 5);
            r.Reason = Field(f, 7);
            r.TransactionDate = ParseDate(Field(f, 10));
            r.MerchantId = Field(f, 13);

            return r;
        }

        // ---------------------------------------------------------------
        // Double verification (status query)
        // ---------------------------------------------------------------

        /// <summary>Asks SBI ePay for the authoritative status of an order.</summary>
        public static SbiResult QueryStatus(string orderNo, decimal amount)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            // queryRequest = ATRN(optional)|Merchant ID|Merchant Order Number|Amount
            string body =
                "queryRequest=" + HttpUtility.UrlEncode("|" + MerchantId + "|" + orderNo + "|" + FormatAmount(amount)) +
                "&aggregatorId=" + HttpUtility.UrlEncode(Setting("SBIAggregatorId")) +
                "&merchantId=" + HttpUtility.UrlEncode(MerchantId);

            byte[] data = Encoding.ASCII.GetBytes(body);

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(Setting("SBIDoubleVerificationUrl"));

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;
            request.Timeout = HttpTimeoutMs;
            request.ReadWriteTimeout = HttpTimeoutMs;

            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            using (WebResponse response = request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return ParseStatusQueryResponse(reader.ReadToEnd());
            }
        }

        /// <summary>
        /// Merchant ID|ATRN|Status|Country|Currency|Other Details|Order No|Amount|
        /// Status Description|Bank Code|Bank Ref|Transaction Date|Pay Mode|CIN|...
        /// </summary>
        public static SbiResult ParseStatusQueryResponse(string raw)
        {
            raw = (raw ?? "").Trim();

            SbiResult r = new SbiResult { Raw = raw };
            string[] f = raw.Split('|');

            if (f.Length >= 9)
            {
                r.MerchantId = Field(f, 0);
                r.Atrn = Field(f, 1);
                r.RawStatus = Field(f, 2);
                r.OrderNo = Field(f, 6);
                r.Amount = ParseAmount(Field(f, 7));
                r.Reason = Field(f, 8);
                r.TransactionDate = ParseDate(Field(f, 11));
                r.PayMode = Field(f, 12);
                r.Status = MapStatus(r.RawStatus);
            }

            // Short or irregular replies (for example when nothing is found):
            // look for a "no records" marker anywhere in the reply.
            if (r.Status == SbiStatus.Unknown
                && raw.IndexOf("NO RECORD", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                r.RawStatus = "NO RECORDS FOUND";
                r.Status = SbiStatus.NotFound;
            }

            return r;
        }

        /// <summary>
        /// SBI statuses: SUCCESS, FAIL, PENDING, ABORT, NO RECORDS FOUND, EXPIRED,
        /// CANCELLED, REFUND, BOOKED and INPROGRESS.
        /// </summary>
        public static SbiStatus MapStatus(string status)
        {
            switch ((status ?? "").Trim().ToUpperInvariant())
            {
                case "SUCCESS":
                    return SbiStatus.Success;

                case "FAIL":
                case "FAILED":
                case "ABORT":
                case "EXPIRED":
                case "CANCELLED":
                case "REFUND":
                    return SbiStatus.Failed;

                case "PENDING":
                case "BOOKED":
                case "INPROGRESS":
                case "IN PROGRESS":
                    return SbiStatus.Pending;

                case "NO RECORDS FOUND":
                case "NO RECORD FOUND":
                    return SbiStatus.NotFound;

                default:
                    return SbiStatus.Unknown;
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static string EncryptionKey()
        {
            string key = ConfigurationManager.AppSettings["SBIEncryptionKey"];

            if (string.IsNullOrWhiteSpace(key) || key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
                || key == "CHANGE_ME")
            {
                throw new ConfigurationErrorsException(
                    "SBIEncryptionKey is not set in appSettings.secrets.config.");
            }

            return key;
        }

        private static string Setting(string key)
        {
            string value = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrWhiteSpace(value))
                throw new ConfigurationErrorsException("App setting '" + key + "' is not set.");

            return value.Trim();
        }

        private static string Field(string[] fields, int index)
        {
            return index < fields.Length ? fields[index].Trim() : "";
        }

        private static decimal? ParseAmount(string value)
        {
            decimal amount;

            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
                ? amount
                : (decimal?)null;
        }

        private static DateTime? ParseDate(string value)
        {
            DateTime date;

            return DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                ? date
                : (DateTime?)null;
        }
    }
}
