<%@ Page Title="Result - OMR Result Portal" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Result.aspx.cs" Inherits="KPSCOmrResult.Result" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="css/Result.css?v=1" rel="stylesheet" />

    <div class="result-page mt-3">

        <div class="text-center mb-2">
            <h2 class="result-title"><asp:Literal ID="litExamName" runat="server" /> RESULT</h2>
            <asp:Label ID="lblStatus" runat="server" CssClass="result-status" />
        </div>

        <div class="card shadow">
            <div class="card-header text-white bg-primary text-center">
                <h5 class="mb-0 fs-5"><i class="bi bi-table"></i> Candidate Results</h5>
            </div>
            <div class="card-body">
                <%-- The results table and one details popup per paper (Result.aspx.cs).
                     Every value is HTML-encoded before it is written. --%>
                <asp:Literal ID="litResults" runat="server" />
            </div>
        </div>

    </div>
</asp:Content>
