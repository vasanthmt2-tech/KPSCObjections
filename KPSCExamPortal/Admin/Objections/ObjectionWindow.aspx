<%@ Page Title="Objection Window - Answer Key Objection Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="ObjectionWindow.aspx.cs" Inherits="KPSCExamPortal.Admin.Objections.EditObjectionWindow" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="container mt-4 mb-5" style="max-width: 760px;">

        <h2 class="h3">Objection Window</h2>
        <p class="text-muted">
            Candidates can add objections and pay the fee only between these times.
            A change applies at once. Payments already started are still verified after the window closes.
        </p>

        <asp:Label ID="lblNoExam" runat="server" CssClass="text-danger fw-bold" Visible="false"
            Text="No examination has been set up yet." />

        <asp:Label ID="lblMessage" runat="server" />

        <asp:Panel ID="pnlForm" runat="server" DefaultButton="btnSave">

            <div class="mb-3">
                <label class="form-label fw-bold" for="<%= ddlExam.ClientID %>">Examination</label>
                <asp:DropDownList ID="ddlExam" runat="server" CssClass="form-select"
                    AutoPostBack="true" OnSelectedIndexChanged="ddlExam_SelectedIndexChanged" />
            </div>

            <div class="card mb-4">
                <div class="card-body">
                    <div class="d-flex flex-wrap align-items-center gap-2 mb-2">
                        <span class="fw-bold">Current status:</span>
                        <asp:Label ID="lblStatus" runat="server" />
                    </div>
                    <div class="mb-1"><span class="text-muted">Current window:</span> <asp:Label ID="lblCurrent" runat="server" /></div>
                    <div><span class="text-muted">Server time now:</span> <asp:Label ID="lblNow" runat="server" /></div>
                </div>
            </div>

            <div class="row g-3">
                <div class="col-12 col-md-6">
                    <fieldset class="border rounded p-3 h-100">
                        <legend class="float-none w-auto px-2 fs-6 fw-bold mb-0">Opens</legend>
                        <label class="form-label" for="<%= txtStartDate.ClientID %>">Date</label>
                        <asp:TextBox ID="txtStartDate" runat="server" TextMode="Date" CssClass="form-control mb-2" />
                        <label class="form-label" for="<%= txtStartTime.ClientID %>">Time</label>
                        <asp:TextBox ID="txtStartTime" runat="server" TextMode="Time" CssClass="form-control" />
                    </fieldset>
                </div>
                <div class="col-12 col-md-6">
                    <fieldset class="border rounded p-3 h-100">
                        <legend class="float-none w-auto px-2 fs-6 fw-bold mb-0">Closes</legend>
                        <label class="form-label" for="<%= txtEndDate.ClientID %>">Date</label>
                        <asp:TextBox ID="txtEndDate" runat="server" TextMode="Date" CssClass="form-control mb-2" />
                        <label class="form-label" for="<%= txtEndTime.ClientID %>">Time</label>
                        <asp:TextBox ID="txtEndTime" runat="server" TextMode="Time" CssClass="form-control" />
                        <div class="form-text">The window stays open until the end of the minute chosen (e.g. 11:59 PM means 11:59:59 PM).</div>
                    </fieldset>
                </div>
            </div>

            <div class="d-flex gap-2 mt-4">
                <asp:Button ID="btnSave" runat="server" Text="Save" CssClass="btn btn-primary"
                    OnClick="btnSave_Click"
                    OnClientClick="return confirm('Save the objection window? Candidates will see the change at once.');" />
                <asp:Button ID="btnCancel" runat="server" Text="Discard changes" CssClass="btn btn-outline-secondary"
                    CausesValidation="false" OnClick="btnCancel_Click" />
                <a class="btn btn-link ms-auto" href="<%= ResolveUrl("~/Admin/Objections/Dashboard.aspx") %>">Back to Dashboard</a>
            </div>

        </asp:Panel>
    </div>

</asp:Content>
