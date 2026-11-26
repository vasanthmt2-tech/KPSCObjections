<%@ Page Title="Objection Windows - KPSC Examination Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="ObjectionWindow.aspx.cs" Inherits="KPSCExamPortal.Admin.Objections.EditObjectionWindow" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="container mt-4 mb-5" style="max-width: 1100px;">

        <h2 class="h3">Objection Windows</h2>
        <p class="text-muted">
            Answer key objections and marks objections each have their own window. Candidates can file
            (and, for answer key objections, pay) only while that window is open. A change applies at once.
            Payments already started are still verified after the window closes.
        </p>

        <asp:Label ID="lblNoExam" runat="server" CssClass="text-danger fw-bold" Visible="false"
            Text="No examination has been set up yet." />

        <asp:Label ID="lblMessage" runat="server" />

        <asp:Panel ID="pnlForm" runat="server">

            <div class="d-flex flex-wrap align-items-end gap-3 mb-4">
                <div style="min-width: 280px;">
                    <label class="form-label fw-bold" for="<%= ddlExam.ClientID %>">Examination</label>
                    <asp:DropDownList ID="ddlExam" runat="server" CssClass="form-select"
                        AutoPostBack="true" OnSelectedIndexChanged="ddlExam_SelectedIndexChanged" />
                </div>
                <div class="ms-auto text-muted pb-2">
                    Server time now: <asp:Label ID="lblNow" runat="server" CssClass="fw-bold text-body" />
                </div>
            </div>

            <div class="row g-4">

                <%-- Answer key objection window: dbo.Exam.ObjectionStart / ObjectionEnd --%>
                <asp:Panel ID="pnlKey" runat="server" CssClass="col-12 col-lg-6" DefaultButton="btnSaveKey">
                    <div class="card h-100" id="key-window">
                        <div class="card-header d-flex align-items-center gap-2">
                            <span class="fw-bold me-auto">Answer Key Objection</span>
                            <asp:Label ID="lblKeyStatus" runat="server" />
                        </div>
                        <div class="card-body">
                            <div class="mb-3"><span class="text-muted">Current window:</span> <asp:Label ID="lblKeyCurrent" runat="server" /></div>

                            <div class="row g-2 mb-2">
                                <div class="col-12 fw-bold">Opens</div>
                                <div class="col-7">
                                    <label class="form-label small mb-1" for="<%= txtKeyStartDate.ClientID %>">Date</label>
                                    <asp:TextBox ID="txtKeyStartDate" runat="server" TextMode="Date" CssClass="form-control" />
                                </div>
                                <div class="col-5">
                                    <label class="form-label small mb-1" for="<%= txtKeyStartTime.ClientID %>">Time</label>
                                    <asp:TextBox ID="txtKeyStartTime" runat="server" TextMode="Time" CssClass="form-control" />
                                </div>
                            </div>
                            <div class="row g-2 mb-3">
                                <div class="col-12 fw-bold">Closes</div>
                                <div class="col-7">
                                    <label class="form-label small mb-1" for="<%= txtKeyEndDate.ClientID %>">Date</label>
                                    <asp:TextBox ID="txtKeyEndDate" runat="server" TextMode="Date" CssClass="form-control" />
                                </div>
                                <div class="col-5">
                                    <label class="form-label small mb-1" for="<%= txtKeyEndTime.ClientID %>">Time</label>
                                    <asp:TextBox ID="txtKeyEndTime" runat="server" TextMode="Time" CssClass="form-control" />
                                </div>
                            </div>

                            <asp:Button ID="btnSaveKey" runat="server" Text="Save answer key window" CssClass="btn btn-primary"
                                OnClick="btnSaveKey_Click"
                                OnClientClick="return confirm('Save the answer key objection window? Candidates will see the change at once.');" />
                        </div>
                    </div>
                </asp:Panel>

                <%-- Marks objection window: dbo.Exam.MarksObjectionStart / MarksObjectionEnd --%>
                <asp:Panel ID="pnlMarks" runat="server" CssClass="col-12 col-lg-6" DefaultButton="btnSaveMarks">
                    <div class="card h-100" id="marks-window">
                        <div class="card-header d-flex align-items-center gap-2">
                            <span class="fw-bold me-auto">Marks Objection</span>
                            <asp:Label ID="lblMarksStatus" runat="server" />
                        </div>
                        <div class="card-body">
                            <div class="mb-3"><span class="text-muted">Current window:</span> <asp:Label ID="lblMarksCurrent" runat="server" /></div>

                            <div class="row g-2 mb-2">
                                <div class="col-12 fw-bold">Opens</div>
                                <div class="col-7">
                                    <label class="form-label small mb-1" for="<%= txtMarksStartDate.ClientID %>">Date</label>
                                    <asp:TextBox ID="txtMarksStartDate" runat="server" TextMode="Date" CssClass="form-control" />
                                </div>
                                <div class="col-5">
                                    <label class="form-label small mb-1" for="<%= txtMarksStartTime.ClientID %>">Time</label>
                                    <asp:TextBox ID="txtMarksStartTime" runat="server" TextMode="Time" CssClass="form-control" />
                                </div>
                            </div>
                            <div class="row g-2 mb-3">
                                <div class="col-12 fw-bold">Closes</div>
                                <div class="col-7">
                                    <label class="form-label small mb-1" for="<%= txtMarksEndDate.ClientID %>">Date</label>
                                    <asp:TextBox ID="txtMarksEndDate" runat="server" TextMode="Date" CssClass="form-control" />
                                </div>
                                <div class="col-5">
                                    <label class="form-label small mb-1" for="<%= txtMarksEndTime.ClientID %>">Time</label>
                                    <asp:TextBox ID="txtMarksEndTime" runat="server" TextMode="Time" CssClass="form-control" />
                                </div>
                            </div>

                            <asp:Button ID="btnSaveMarks" runat="server" Text="Save marks window" CssClass="btn btn-primary"
                                OnClick="btnSaveMarks_Click"
                                OnClientClick="return confirm('Save the marks objection window? Candidates will see the change at once.');" />
                        </div>
                    </div>
                </asp:Panel>

            </div>

            <div class="form-text mt-3">
                A window stays open until the end of the minute chosen (e.g. 11:59 PM means 11:59:59 PM).
                Saving one window never changes the other.
            </div>

            <div class="d-flex gap-2 mt-4">
                <asp:Button ID="btnCancel" runat="server" Text="Discard changes" CssClass="btn btn-outline-secondary"
                    CausesValidation="false" OnClick="btnCancel_Click" />
                <a class="btn btn-link ms-auto" href="<%= ResolveUrl("~/Admin/Default.aspx") %>">Back to Home</a>
            </div>

        </asp:Panel>
    </div>

</asp:Content>
