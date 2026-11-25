<%@ Page Title="Admin Home - KPSC Examination Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="KPSCExamPortal.Admin._Default" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%: ResolveUrl("~/css/Home.css") %>?v=1" rel="stylesheet" />

    <div class="home-page">
        <h2 class="home-title">Administration</h2>
        <p class="home-intro">Every admin action is recorded in the audit trail.</p>

        <div class="home-cards">
            <div class="home-card">
                <i class="bi bi-people home-icon" aria-hidden="true"></i>
                <h3>Candidates and Window</h3>
                <ul>
                    <li><a href="~/Admin/UploadCandidates.aspx" runat="server">Upload Candidates and Version Codes</a></li>
                    <li><a href="~/Admin/Objections/ObjectionWindow.aspx" runat="server">Objection Window (key answers and marks)</a></li>
                    <li><a href="~/Admin/AuditTrail.aspx" runat="server">Audit Trail</a></li>
                </ul>
            </div>

            <div class="home-card">
                <i class="bi bi-ui-checks home-icon" aria-hidden="true"></i>
                <h3>Answer Key Objections</h3>
                <ul>
                    <li><a href="~/Admin/Objections/Dashboard.aspx" runat="server">Dashboard</a></li>
                    <li><a href="~/Admin/Objections/UploadKeyAnswer.aspx" runat="server">Upload Key Answers</a></li>
                    <li><a href="~/Admin/Objections/AdminObjection.aspx" runat="server">Objections</a></li>
                    <li><a href="~/Admin/Objections/CandidateObjections.aspx" runat="server">Candidate Objections</a></li>
                </ul>
            </div>

            <div class="home-card">
                <i class="bi bi-pencil-square home-icon" aria-hidden="true"></i>
                <h3>Marks Objections</h3>
                <ul>
                    <li><a href="~/Admin/Marks/Dashboard.aspx" runat="server">Dashboard</a></li>
                    <li><a href="~/Admin/Marks/AdminObjection.aspx" runat="server">Objections</a></li>
                    <li><a href="~/Admin/Marks/CandidateObjections.aspx" runat="server">Candidate Objections</a></li>
                </ul>
            </div>

            <div class="home-card">
                <i class="bi bi-table home-icon" aria-hidden="true"></i>
                <h3>OMR Results</h3>
                <ul>
                    <li><a href="~/Admin/Results/UploadResults.aspx" runat="server">Upload Results</a></li>
                    <li><a href="~/Admin/Results/UploadKeyAnswer.aspx" runat="server">Upload Final Key</a></li>
                </ul>
            </div>
        </div>
    </div>
</asp:Content>
