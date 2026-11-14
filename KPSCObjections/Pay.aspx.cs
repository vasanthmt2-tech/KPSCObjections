using System;
using System.Web;
using KPSCObjections.Payments;
using KPSCObjections.Security;

namespace KPSCObjections
{
    /// <summary>
    /// Sends the candidate to SBI ePay for the payment started on the
    /// Objection page. Works once per payment: a refresh goes back to the
    /// Objection page, which shows the payment as being verified.
    /// </summary>
    public partial class Pay : CandidatePage
    {
        protected string PaymentUrl { get; private set; }
        protected string EncryptTrans { get; private set; }
        protected string MerchantId { get; private set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            string orderNo = Session["PayOrderNo"] as string;
            Session.Remove("PayOrderNo");

            ObjectionSession.LoginDetails candidate =
                (ObjectionSession.LoginDetails)Session[AuthSession.CandidateKey];

            PaymentInfo payment = string.IsNullOrEmpty(orderNo) ? null : PaymentService.Find(orderNo);

            if (payment == null
                || payment.Status != PaymentService.Pending
                || !string.Equals(payment.RegNo, candidate.RegNo, StringComparison.OrdinalIgnoreCase))
            {
                Response.Redirect("~/Objection.aspx", true);
                return;
            }

            PaymentUrl = SbiEpay.PaymentUrl;
            MerchantId = SbiEpay.MerchantId;
            EncryptTrans = SbiEpay.BuildEncryptedRequest(payment.OrderNo, payment.Amount, candidate.RegNo);
        }
    }
}
