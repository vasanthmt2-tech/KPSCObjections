using System;
using KPSCObjections.Security;
using System.Web;
using KPSCObjections.Payments;

namespace KPSCObjections
{
    /// <summary>
    /// SBI ePay server-to-server "push response" endpoint. Give SBI this URL
    /// for whitelisting; it must be reachable over HTTPS.
    ///
    /// SBI posts pushRespData (encrypted), merchIdVal and Bank_Code. As with
    /// the browser return, the push only identifies the order; the outcome is
    /// taken from the status query.
    /// </summary>
    public class PaymentPush : IHttpHandler
    {
        public bool IsReusable { get { return true; } }

        public void ProcessRequest(HttpContext context)
        {
            try
            {
                // Junk posts are logged briefly and limited per address (PaymentCallback).
                string orderNo = PaymentCallback.ReadOrderNo(context, "pushRespData", "push");

                if (orderNo != null)
                    PaymentService.Verify(orderNo);
            }
            catch (Exception ex)
            {
                AppLog.Error("Payment push could not be processed: " + ex);
            }

            context.Response.ContentType = "text/plain";
            context.Response.Write("OK");
        }
    }
}
