<%@ Page Title="Result - OMR Result Portal" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Result.aspx.cs" Inherits="KPSCExamPortal.Results.Result" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%: ResolveUrl("~/css/Result.css?v=6") %>" rel="stylesheet" />

    <div class="result-page mt-3">

        <div class="text-center mb-2">
            <h2 class="result-title"><asp:Literal ID="litExamName" runat="server" /> RESULT</h2>
            <asp:Label ID="lblStatus" runat="server" CssClass="result-status" />
        </div>

        <div class="card result-panel">
            <div class="card-header result-panel-header">
                <h3><i class="bi bi-table"></i> Candidate Results</h3>
            </div>
            <div class="card-body">
                <%-- The results table and one details popup per paper (Result.aspx.cs).
                     Every value is HTML-encoded before it is written. --%>
                <asp:Literal ID="litResults" runat="server" />
            </div>
        </div>

    </div>
</asp:Content>
