using System;
using System.Web;
using KPSCExamPortal.Security;

namespace KPSCExamPortal.Payments
{
    /// <summary>
    /// Reads the order number from an SBI ePay callback (the browser return
    /// to PaymentResponse and the server push to PaymentPush.ashx).
    ///
    /// Both endpoints must accept posts from anyone, so junk posts are
    /// expected. A post that cannot be decrypted is logged as one WARN line
    /// (no stack trace), at most a few per address a minute, and an address
    /// that keeps sending them is refused with 429 for the rest of the minute.
    /// Only unreadable posts are counted: SBI's genuine posts always decrypt,
    /// so they are never limited.
    /// </summary>
    public static class PaymentCallback
    {
        private const string Bucket = "bad-payment-callback";

        /// <summary>Unreadable callbacks per address a minute that are logged.</summary>
        private const int LoggedPerMinute = 5;

        /// <summary>
        /// The order number in the posted field, or null when there is none,
        /// it cannot be read, or the address is refused for now.
        /// </summary>
        public static string ReadOrderNo(HttpContext context, string field, string source)
        {
            string data = context.Request.Form[field];

            if (string.IsNullOrWhiteSpace(data))
                return null;

            string ip = RequestThrottle.ClientIp(context);
            int limit = RequestThrottle.Setting("BadPaymentCallbacksPerIpPerMinute", 20);

            if (RequestThrottle.Count(Bucket, ip) >= limit)
            {
                Refuse(context);
                return null;
            }

            string reason;

            try
            {
                string orderNo = SbiEpay.ParseEncryptedResponse(data).OrderNo;

                if (!string.IsNullOrWhiteSpace(orderNo))
                    return orderNo.Trim();

                reason = "no order number";
            }
            catch (Exception ex)
            {
                reason = ex.GetType().Name;
            }

            bool firstRejection;
            bool allowed = RequestThrottle.Allow(Bucket, ip, limit, TimeSpan.FromMinutes(1), out firstRejection);
            int count = RequestThrottle.Count(Bucket, ip);

            if (count <= LoggedPerMinute)
            {
                AppLog.Write("WARN", "Unreadable SBI ePay " + source + " from " + ip + " (" + reason + "); ignored." +
                    (count == LoggedPerMinute ? " Further ones from this address are not logged this minute." : ""));
            }

            // The limit is reached: later posts from this address are refused
            // above, before any work, for the rest of the minute.
            if (count == limit)
            {
                AppLog.Write("WARN", "Too many unreadable SBI ePay callbacks (" + limit + " in a minute) from " +
                             ip + "; further ones refused for the rest of the minute.");
            }

            if (!allowed)
                Refuse(context);

            return null;
        }

        private static void Refuse(HttpContext context)
        {
            context.Response.StatusCode = 429;
            context.Response.TrySkipIisCustomErrors = true;
            context.Response.AppendHeader("Retry-After", "60");
        }
    }
}
