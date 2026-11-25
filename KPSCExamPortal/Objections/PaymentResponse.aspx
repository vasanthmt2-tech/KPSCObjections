<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="PaymentResponse.aspx.cs" Inherits="KPSCExamPortal.Objections.PaymentResponse" EnableSessionState="false" EnableViewState="false" %>
<%-- SBI ePay posts the candidate's browser here (SuccessURL and FailureURL).
     The post arrives from another site, so the login cookies are not sent with
     it; this page therefore does not use the session or the site master. --%>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Payment Status - Answer Key Objection Portal</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet" integrity="sha384-QWTKZyjpPEjISv5WaRU9OFeRpok6YctnYmDr5pNlyT2bRjXh0JMhjY6hW+ALEwIH" crossorigin="anonymous" />
    <link href="<%: ResolveUrl("~/css/PaymentResponse.css") %>" rel="stylesheet" />
    <script src="<%: ResolveUrl("~/Scripts/no-context-menu.js") %>"></script>
</head>
<body>
    <div class="payment-response-page">

        <div class="payment-response-panel">

            <div class="payment-response-icon">
                <asp:Label ID="lblIcon" runat="server"
                    Text="&#9203;" />
            </div>

            <h2>
                <asp:Label ID="lblHeading" runat="server"
                    Text="Payment Status" />
            </h2>

            <p class="response-message">
                <asp:Label ID="lblMessage" runat="server" />
            </p>

            <div class="transaction-details">
                <div class="detail-row">
                    <span>Transaction Reference</span>
                    <asp:Label ID="lblOrderNo" runat="server"
                        Text="-" />
                </div>
            </div>

            <asp:HyperLink ID="lnkContinue"
                runat="server"
                NavigateUrl="~/Objections/MyObjections.aspx"
                CssClass="btn-continue"
                Text="Continue" />

        </div>

    </div>
</body>
</html>
