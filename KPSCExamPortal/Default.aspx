<%@ Page Title="KPSC Examination Portal" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="KPSCExamPortal._Default" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%: ResolveUrl("~/css/Home.css") %>?v=1" rel="stylesheet" />

    <div class="home-page">

        <h2 class="home-title">Candidate Services</h2>
        <p class="home-intro">Sign in once with your registration number and the OTP sent to your registered mobile number,
            then use any of these services.</p>

        <div class="home-cards">

            <div class="home-card">
                <i class="bi bi-ui-checks home-icon" aria-hidden="true"></i>
                <h3>Answer Key Objections</h3>
                <p>Raise objections to the provisional key answers, question by question, with an optional supporting PDF.</p>
                <a id="lnkKey" runat="server" class="btn btn-primary home-button">Sign in with OTP</a>
            </div>

            <div class="home-card">
                <i class="bi bi-pencil-square home-icon" aria-hidden="true"></i>
                <h3>Marks Objection</h3>
                <p>Raise an objection about your marks, one per paper, with an optional supporting PDF.</p>
                <a id="lnkMarks" runat="server" class="btn btn-primary home-button">Sign in with OTP</a>
            </div>

            <div class="home-card">
                <i class="bi bi-table home-icon" aria-hidden="true"></i>
                <h3>OMR Result</h3>
                <p>See your OMR result and the question-wise marks: your answer, the final key and the marks for each question.</p>
                <a id="lnkResult" runat="server" class="btn btn-primary home-button">Sign in with OTP</a>
            </div>

        </div>
    </div>
</asp:Content>
