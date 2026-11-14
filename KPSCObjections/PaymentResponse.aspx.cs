using System;
using System.Web;
using KPSCObjections.Payments;

namespace KPSCObjections
{
    /// <summary>
    /// Landing page after SBI ePay (configured as both SuccessURL and FailureURL).
    ///
    /// The posted 'encData' only tells us which order came back. The outcome
    /// shown and recorded always comes from SBI ePay's status query, so a
    /// forged or replayed post cannot mark a payment as paid.
    /// </summary>
    public partial class PaymentResponse : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            string orderNo = null;
            string status = null;

            try
            {
                string encData = Request.Form["encData"];

                if (!string.IsNullOrWhiteSpace(encData))
                {
                    orderNo = SbiEpay.ParseEncryptedResponse(encData).OrderNo;
                    status = PaymentService.Verify(orderNo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Payment response could not be processed: " + ex.Message);
            }

            if (status == null)
            {
                Show("&#9888;", "Payment Status",
                    "We could not read the payment response. If you completed a payment, " +
                    "log in and open File Objection to check its status.", null);
                return;
            }

            PaymentInfo payment = PaymentService.Find(orderNo);
            string reference = string.IsNullOrEmpty(payment.SbiReferenceNo) ? payment.OrderNo : payment.SbiReferenceNo;

            if (status == PaymentService.Success)
            {
                Show("&#9989;", "Payment Successful",
                    "Your payment of Rs. " + SbiEpay.FormatAmount(payment.Amount) +
                    " was received and your objections are submitted.", reference);
            }
            else if (status == PaymentService.Failed)
            {
                Show("&#10060;", "Payment Not Completed",
                    "The payment was not completed. Your objections are saved; " +
                    "open File Objection and click Pay Now to pay again.", reference);

                lnkContinue.NavigateUrl = "~/Objection.aspx";
            }
            else
            {
                Show("&#9203;", "Payment Being Verified",
                    "Your payment is not confirmed yet. Please do not pay again. " +
                    "Open File Objection after some time to check the status.", reference);

                lnkContinue.NavigateUrl = "~/Objection.aspx";
            }
        }

        private void Show(string icon, string heading, string message, string reference)
        {
            lblIcon.Text = icon;
            lblHeading.Text = heading;
            lblMessage.Text = HttpUtility.HtmlEncode(message);
            lblOrderNo.Text = string.IsNullOrEmpty(reference) ? "-" : HttpUtility.HtmlEncode(reference);
        }
    }
}
