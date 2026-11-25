<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Pay.aspx.cs" Inherits="KPSCExamPortal.Objections.Pay" EnableViewState="false" %>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Redirecting to SBI ePay</title>
    <script src="<%: ResolveUrl("~/Scripts/no-context-menu.js") %>"></script>
</head>
    <body onload="document.getElementById('sbiForm').submit();">
        <p style="font-family: sans-serif; text-align: center; margin-top: 80px;">
            Redirecting to SBI ePay. Please do not refresh this page or press Back.
        </p>

        <%-- Posted straight to SBI ePay. Every value is built on the server from the
             payment record; the candidate cannot edit the amount. --%>
        <form id="sbiForm" method="post" action="<%: PaymentUrl %>">
            <input type="hidden" name="EncryptTrans" value="<%: EncryptTrans %>" />
            <input type="hidden" name="merchIdVal" value="<%: MerchantId %>" />
            <noscript>
                <p style="text-align: center;"><input type="submit" value="Continue to payment" /></p>
            </noscript>
        </form>
    </body>
</html>
